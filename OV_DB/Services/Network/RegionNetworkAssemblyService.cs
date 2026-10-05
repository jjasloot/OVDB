using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using NetTopologySuite.Geometries;
using NetTopologySuite.Simplify;
using Newtonsoft.Json;
using OV_DB.Hubs;
using OVDB_database.Database;
using OVDB_database.Models;

namespace OV_DB.Services.Network;

public sealed record NetworkAssemblyRequest(int RegionId, bool RefreshFromOsm);

/// <summary>
/// Assembles a region's rail networks (both modes) from OSM in the background: three cached
/// Overpass fetches, classification by route relations, topology, dedup, splitting, and
/// publication as <see cref="NetworkEdge"/> rows together with every route's match rows.
/// Modelled on <see cref="UpdateRegionService"/>: a static queue, one scope per item, one failure
/// never kills the loop, progress over SignalR. A network that was published before stays
/// readable at its old version throughout, and after a failed run.
/// </summary>
public class RegionNetworkAssemblyService(
    IServiceProvider serviceProvider,
    IHubContext<MapGenerationHub> hubContext,
    IOverpassService overpassService,
    INetworkMatchIndexCache indexCache,
    IConfiguration configuration,
    ILogger<RegionNetworkAssemblyService> logger) : IHostedService, IDisposable
{
    private static readonly ConcurrentQueue<int> Queue = new();
    /// <summary>Regions waiting in <see cref="Queue"/>, with whether any request for them asked for a refetch.</summary>
    private static readonly ConcurrentDictionary<int, bool> Pending = new();

    /// <summary>
    /// Queue a run for a region. Requests for a region that is already waiting merge into that
    /// run (a refetch wins): undoing an override way by way, or a burst of curation clicks, then
    /// costs one reassembly instead of one per click. A request arriving while the region is
    /// being assembled queues a fresh run, because the running one may have read stale overrides.
    /// </summary>
    public static void Enqueue(int regionId, bool refreshFromOsm)
    {
        while (true)
        {
            if (Pending.TryAdd(regionId, refreshFromOsm))
            {
                Queue.Enqueue(regionId);
                return;
            }
            // Already waiting: merge. Losing a race to the dequeue just means trying to add again;
            // a run that dequeued the region reads the overrides afterwards, so it sees this change.
            if (Pending.TryGetValue(regionId, out var refresh) && Pending.TryUpdate(regionId, refresh || refreshFromOsm, refresh))
                return;
        }
    }

    internal static bool TryDequeue(out NetworkAssemblyRequest request)
    {
        request = null;
        if (!Queue.TryDequeue(out var regionId))
            return false;
        Pending.TryRemove(regionId, out var refresh);
        request = new NetworkAssemblyRequest(regionId, refresh);
        return true;
    }

    private const int PersistBatchSize = 500;
    private const double SimplifyToleranceDeg = 0.0001; // ≈ 10 m
    private const string InterruptedError = "Interrupted by an application restart; assemble again.";

    private static readonly GeometryFactory Factory = new(new PrecisionModel(), 4326);

    private Task _backgroundTask;
    private CancellationTokenSource _cancellationTokenSource;

    public Task StartAsync(CancellationToken cancellationToken)
    {
        _cancellationTokenSource = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        _backgroundTask = Task.Run(() => ProcessQueueAsync(_cancellationTokenSource.Token));
        return Task.CompletedTask;
    }

    /// <summary>
    /// The queue lives in memory, so a run that was queued or in progress when the app stopped is
    /// gone. Say so rather than showing it as running forever; the published data is unaffected.
    /// </summary>
    private async Task ResetInterruptedRunsAsync(CancellationToken cancellationToken)
    {
        try
        {
            using var scope = serviceProvider.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<OVDBDatabaseContext>();
            var reset = await db.RegionNetworks
                .Where(n => n.Status == NetworkStatus.Queued || n.Status == NetworkStatus.Assembling)
                .ExecuteUpdateAsync(s => s
                    .SetProperty(n => n.Status, NetworkStatus.Failed)
                    .SetProperty(n => n.Error, InterruptedError), cancellationToken);
            if (reset > 0)
                logger.LogWarning("Marked {Count} network assembly runs interrupted by the last shutdown as failed", reset);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogError(ex, "Could not reset interrupted network assembly runs");
        }
    }

    private async Task ProcessQueueAsync(CancellationToken cancellationToken)
    {
        await ResetInterruptedRunsAsync(cancellationToken);
        while (!cancellationToken.IsCancellationRequested)
        {
            if (TryDequeue(out var request))
            {
                try
                {
                    await AssembleAsync(request, cancellationToken);
                }
                catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
                {
                    break;
                }
                catch (Exception ex)
                {
                    logger.LogError(ex, "Failed to assemble the networks of region {RegionId}; continuing with the queue", request.RegionId);
                    await MarkFailedAsync(request.RegionId, ex.Message);
                    await Report(request.RegionId, 100, cancellationToken);
                }
            }
            else
            {
                try
                {
                    await Task.Delay(1000, cancellationToken);
                }
                catch (OperationCanceledException)
                {
                    break;
                }
            }
        }
    }

    public async Task AssembleAsync(NetworkAssemblyRequest request, CancellationToken cancellationToken = default)
    {
        using var scope = serviceProvider.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<OVDBDatabaseContext>();

        var region = await db.Regions
            .Where(r => r.Id == request.RegionId)
            .Select(r => new { r.Id, r.Name, r.OsmRelationId, r.Geometry })
            .FirstOrDefaultAsync(cancellationToken)
            ?? throw new InvalidOperationException($"Region {request.RegionId} does not exist");

        var networks = await EnsureNetworkRowsAsync(db, region.Id, cancellationToken);
        await Report(region.Id, 0, cancellationToken);

        // 1. Fetch (cached) and parse.
        var cacheDir = Path.Combine(CacheRoot(), region.Id.ToString());
        var areaId = region.OsmRelationId + 3_600_000_000;
        var trackPath = await GetOrFetchAsync(Path.Combine(cacheDir, "track.json"), TrackQuery(areaId), request.RefreshFromOsm, cancellationToken);
        var stopsPath = await GetOrFetchAsync(Path.Combine(cacheDir, "stops.json"), StopsQuery(areaId), request.RefreshFromOsm, cancellationToken);
        var routesPath = await GetOrFetchAsync(Path.Combine(cacheDir, "routes.json"), RoutesQuery(areaId), request.RefreshFromOsm, cancellationToken);
        await Report(region.Id, 20, cancellationToken);

        OsmRailData track;
        await using (var stream = File.OpenRead(trackPath))
            track = OsmRailParser.Parse(stream);
        OsmRailData stops;
        await using (var stream = File.OpenRead(stopsPath))
            stops = OsmRailParser.Parse(stream);
        Dictionary<long, PtService> ptServices;
        await using (var stream = File.OpenRead(routesPath))
            ptServices = PtRouteParser.Parse(stream);

        var railStations = await db.Stations
            .Where(s => s.Regions.Any(r => r.Id == region.Id))
            .Select(s => new { s.Longitude, s.Lattitude, s.Name })
            .ToListAsync(cancellationToken);
        var overrides = await db.NetworkWayOverrides
            .Where(o => o.RegionId == region.Id)
            .ToDictionaryAsync(o => o.OsmWayId, o => o.Kind, cancellationToken);

        // 2. Assemble.
        var input = new NetworkAssemblyInput
        {
            Track = track,
            RegionGeometry = region.Geometry,
            PtServices = ptServices,
            Overrides = overrides,
            RailStations = railStations.Select(s => new NetworkStop(new Coordinate(s.Longitude, s.Lattitude), s.Name)).ToList(),
            UrbanStops = stops.Stops.Select(s => new NetworkStop(new Coordinate(s.Lon, s.Lat), s.Name)).ToList(),
        };
        var built = NetworkTopologyBuilder.Build(input, p => _ = Report(region.Id, 20 + p * 60 / 100, cancellationToken));
        logger.LogInformation("Assembled {Region}: {Rail} rail edges, {Urban} urban edges (relations: {ByRelations})",
            region.Name, built.Edges.GetValueOrDefault(NetworkMode.Rail)?.Count ?? 0,
            built.Edges.GetValueOrDefault(NetworkMode.UrbanRail)?.Count ?? 0, built.ClassifiedByRelations);
        await Report(region.Id, 80, cancellationToken);

        // 3. Publish, one mode at a time: edges and every route's match rows in one transaction,
        //    so readers switch from the old version to the new one with its coverage complete.
        //    The write lock keeps the match queue from interleaving route rematches.
        var matcher = scope.ServiceProvider.GetRequiredService<INetworkMatchService>();
        await NetworkWriteLock.Instance.WaitAsync(cancellationToken);
        try
        {
            for (var i = 0; i < networks.Count; i++)
            {
                var network = networks[i];
                var drafts = built.Edges.GetValueOrDefault(network.Mode) ?? new List<NetworkEdgeDraft>();
                var span = 20 / networks.Count;
                var start = 80 + i * span;
                await PublishAsync(db, matcher, network.Id, drafts, built, track.OsmTimestamp,
                    // Capped below the span's end: 100 is reserved for "committed, reload now".
                    p => Report(region.Id, start + Math.Min(p, 99) * span / 100, cancellationToken), cancellationToken);
            }
        }
        finally
        {
            NetworkWriteLock.Instance.Release();
        }
        await Report(region.Id, 100, cancellationToken);
    }

    private static async Task<List<RegionNetwork>> EnsureNetworkRowsAsync(OVDBDatabaseContext db, int regionId, CancellationToken cancellationToken)
    {
        var networks = await db.RegionNetworks.Where(n => n.RegionId == regionId).ToListAsync(cancellationToken);
        foreach (var mode in new[] { NetworkMode.Rail, NetworkMode.UrbanRail })
        {
            if (networks.All(n => n.Mode != mode))
            {
                var created = new RegionNetwork { RegionId = regionId, Mode = mode };
                db.RegionNetworks.Add(created);
                networks.Add(created);
            }
        }
        // Status only describes this run; readers go by Version, so published data stays visible.
        foreach (var network in networks)
            network.Status = NetworkStatus.Assembling;
        await db.SaveChangesAsync(cancellationToken);
        var ordered = networks.OrderBy(n => n.Mode).ToList();
        db.ChangeTracker.Clear();
        return ordered;
    }

    /// <summary>
    /// Replace a network's edges and all its match rows and bump its version, as one unit.
    /// The app's DbContext retries transient failures, which only works with a user transaction
    /// when the whole unit runs through the execution strategy; each attempt therefore starts
    /// from a clean change tracker and creates fresh entities.
    /// </summary>
    private async Task PublishAsync(OVDBDatabaseContext db, INetworkMatchService matcher, int networkId, List<NetworkEdgeDraft> drafts,
        NetworkAssemblyResult built, string osmTimestamp, Func<int, Task> progress, CancellationToken cancellationToken)
    {
        NetworkMatchIndex index = null;
        var version = 0;
        var strategy = db.Database.CreateExecutionStrategy();
        await strategy.ExecuteAsync(async () =>
        {
            db.ChangeTracker.Clear();
            await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);

            // Old edges go first; the database-level cascade takes their match rows with them.
            await db.NetworkEdges.Where(e => e.RegionNetworkId == networkId).ExecuteDeleteAsync(cancellationToken);

            var batch = new List<NetworkEdge>(PersistBatchSize);
            foreach (var draft in drafts)
            {
                batch.Add(ToEntity(draft, networkId, built.Projection));
                if (batch.Count == PersistBatchSize)
                {
                    db.NetworkEdges.AddRange(batch);
                    await db.SaveChangesAsync(cancellationToken);
                    db.ChangeTracker.Clear();
                    batch.Clear();
                }
            }
            if (batch.Count > 0)
            {
                db.NetworkEdges.AddRange(batch);
                await db.SaveChangesAsync(cancellationToken);
                db.ChangeTracker.Clear();
            }

            var network = await db.RegionNetworks.FirstAsync(n => n.Id == networkId, cancellationToken);
            network.EdgeCount = drafts.Count;
            network.TotalKm = Math.Round(drafts.Where(d => d.IsPassenger).Sum(d => d.LengthKm), 3);
            network.BonusKm = Math.Round(drafts.Where(d => !d.IsPassenger).Sum(d => d.LengthKm), 3);
            network.ClassifiedByRelations = built.ClassifiedByRelations;
            network.OsmDataTimestamp = osmTimestamp;
            network.AssembledAt = DateTime.UtcNow;
            network.Version++;
            network.Status = NetworkStatus.Ready;
            network.Error = null;
            await db.SaveChangesAsync(cancellationToken);
            db.ChangeTracker.Clear();

            // Match against the uncommitted new edges, inside the same transaction.
            index = await NetworkMatchIndexCache.BuildAsync(db, network, cancellationToken);
            await matcher.WriteMatchesAsync(network, index, progress, cancellationToken);

            await transaction.CommitAsync(cancellationToken);
            version = network.Version;
        });
        db.ChangeTracker.Clear();
        // Only now does the index describe committed data.
        indexCache.Store(networkId, version, index);
    }

    private static NetworkEdge ToEntity(NetworkEdgeDraft draft, int networkId, NetworkGeo.LocalProjection projection)
    {
        var geometry = Factory.CreateLineString(draft.Geometry.Coordinates);
        var simplified = DouglasPeuckerSimplifier.Simplify(geometry, SimplifyToleranceDeg) as LineString ?? geometry;
        var projected = projection.Project(geometry.Coordinates);
        return new NetworkEdge
        {
            RegionNetworkId = networkId,
            Geometry = geometry,
            SimplifiedGeometry = simplified,
            HiddenTwins = draft.HiddenTwins is { Count: > 0 }
                ? Factory.CreateMultiLineString(draft.HiddenTwins.Select(t => Factory.CreateLineString(t)).ToArray())
                : null,
            LengthKm = draft.LengthKm,
            SampleCount = NetworkLattice.SampleCount(NetworkLattice.PolylineLength(projected)),
            IsPassenger = draft.IsPassenger,
            Name = draft.Name,
            FromStation = draft.FromStation,
            ToStation = draft.ToStation,
            OsmWayIds = JsonConvert.SerializeObject(draft.OsmWayIds),
        };
    }

    private async Task MarkFailedAsync(int regionId, string error)
    {
        try
        {
            // Only the networks this run had not yet published: a mode that already committed is
            // Ready, and every network keeps serving its last published version either way.
            using var scope = serviceProvider.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<OVDBDatabaseContext>();
            await db.RegionNetworks
                .Where(n => n.RegionId == regionId && (n.Status == NetworkStatus.Queued || n.Status == NetworkStatus.Assembling))
                .ExecuteUpdateAsync(s => s
                    .SetProperty(n => n.Status, NetworkStatus.Failed)
                    .SetProperty(n => n.Error, error.Length > 1000 ? error[..1000] : error));
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Could not record the assembly failure of region {RegionId}", regionId);
        }
    }

    /// <summary>Progress is best effort: a SignalR failure must never fail a run or stop the queue.</summary>
    private async Task Report(int regionId, int progress, CancellationToken cancellationToken)
    {
        try
        {
            await hubContext.Clients.All.SendAsync(MapGenerationHub.NetworkUpdateMethod, regionId, progress, cancellationToken);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogWarning(ex, "Could not report assembly progress of region {RegionId}", regionId);
        }
    }

    // ---- Overpass -----------------------------------------------------------------------------

    private string CacheRoot() =>
        configuration["Networks:CachePath"] ?? Path.Combine(AppContext.BaseDirectory, "network-cache");

    /// <summary>
    /// Every rail-bound way, no usage filter (classification happens afterwards), service tracks
    /// excluded: yards, sidings, spurs and crossovers are the main noise source.
    /// </summary>
    private static string TrackQuery(long areaId) =>
        $"[out:json][timeout:900];area({areaId})->.searchArea;" +
        "(way[\"railway\"~\"^(rail|narrow_gauge|preserved|light_rail|subway|tram)$\"][!\"service\"](area.searchArea););" +
        "out body;>;out skel qt;";

    /// <summary>Tram stops and metro/light-rail stations: the Stations table deliberately excludes these.</summary>
    private static string StopsQuery(long areaId) =>
        $"[out:json][timeout:300];area({areaId})->.searchArea;" +
        "(node[\"railway\"=\"tram_stop\"](area.searchArea);" +
        "node[\"railway\"=\"station\"][\"station\"=\"subway\"](area.searchArea);" +
        "node[\"railway\"=\"station\"][\"station\"=\"light_rail\"](area.searchArea););" +
        "out body;";

    /// <summary>Public-transport route relations, member lists only: the classification source.</summary>
    private static string RoutesQuery(long areaId) =>
        $"[out:json][timeout:600];area({areaId})->.searchArea;" +
        "relation[\"route\"~\"^(train|light_rail|subway|tram)$\"](area.searchArea);" +
        "out body;";

    /// <summary>
    /// Overpass is asked once per region and query; the raw response lives on disk until an
    /// explicit refresh. Reassembly after a curation change therefore costs no Overpass call.
    /// </summary>
    private async Task<string> GetOrFetchAsync(string path, string query, bool refresh, CancellationToken cancellationToken)
    {
        if (!refresh && File.Exists(path))
            return path;
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);

        // Each attempt already fails over across all mirrors; the delays only need to outlast a
        // typical endpoint cooldown (same pattern as UpdateRegionService).
        string response = null;
        for (var attempt = 1; attempt <= 3 && response == null; attempt++)
        {
            if (attempt > 1)
                await Task.Delay(TimeSpan.FromSeconds(30 * (attempt - 1)), cancellationToken);
            try
            {
                var candidate = await overpassService.QueryAsync(query, cancellationToken);
                if (IsCompleteResponse(candidate, out var problem))
                    response = candidate;
                else
                    logger.LogWarning("Overpass returned an unusable response (attempt {Attempt}): {Problem}", attempt, problem);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                logger.LogWarning(ex, "Overpass query failed (attempt {Attempt})", attempt);
            }
        }
        if (response == null)
            throw new InvalidOperationException("Overpass did not return a usable response after three attempts");

        var tempPath = path + ".tmp";
        await File.WriteAllTextAsync(tempPath, response, cancellationToken);
        File.Move(tempPath, path, overwrite: true);
        return path;
    }

    private static readonly System.Text.RegularExpressions.Regex RemarkPattern =
        new("\"remark\"\\s*:\\s*\"([^\"]*)\"", System.Text.RegularExpressions.RegexOptions.Compiled);

    /// <summary>
    /// Overpass reports a query timeout or memory exhaustion with HTTP 200 and well-formed JSON
    /// whose <c>elements</c> are empty or cut short, plus a top-level <c>remark</c> saying
    /// "runtime error". Caching that would publish a network with track missing, and keep
    /// republishing it from the cache, so such a response is a failed attempt. The remark comes
    /// after the elements, so only the tail of a (possibly very large) body is searched.
    /// </summary>
    internal static bool IsCompleteResponse(string body, out string problem)
    {
        problem = null;
        if (string.IsNullOrWhiteSpace(body) || !body.Contains("\"elements\"") || !body.TrimEnd().EndsWith('}'))
        {
            problem = "empty or truncated";
            return false;
        }
        var tail = body.Length > 4096 ? body[^4096..] : body;
        var remark = RemarkPattern.Match(tail);
        if (remark.Success && remark.Groups[1].Value.Contains("error", StringComparison.OrdinalIgnoreCase))
        {
            problem = remark.Groups[1].Value;
            return false;
        }
        return true;
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        _cancellationTokenSource?.Cancel();
        return _backgroundTask == null ? Task.CompletedTask : Task.WhenAny(_backgroundTask, Task.Delay(Timeout.Infinite, cancellationToken));
    }

    public void Dispose()
    {
        _cancellationTokenSource?.Cancel();
    }
}
