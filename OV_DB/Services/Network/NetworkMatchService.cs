using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using OVDB_database.Database;
using OVDB_database.Models;
using OV_DB.Services;

namespace OV_DB.Services.Network;

public abstract record NetworkMatchWorkItem;
/// <summary>A route was created, its geometry, type or regions changed: rematch it against every network of its regions.</summary>
public sealed record RouteChanged(int RouteId) : NetworkMatchWorkItem;
/// <summary>Rematch every eligible route of a network's region against it (an admin rematch).</summary>
public sealed record NetworkChanged(int RegionNetworkId) : NetworkMatchWorkItem;

/// <summary>
/// Serialises every write to network edges and match rows across the two background queues.
/// Without it, a route rematch running while its region is republished either writes rows for
/// edges the publish is deleting (and fails on the foreign key) or commits just before the
/// publish's own match pass read the route, losing the change. Writes are rare and short next
/// to how often the data is read, so one lock costs nothing noticeable.
/// </summary>
public static class NetworkWriteLock
{
    public static readonly SemaphoreSlim Instance = new(1, 1);
}

public interface INetworkMatchIndexCache
{
    Task<NetworkMatchIndex> GetAsync(OVDBDatabaseContext db, RegionNetwork network, CancellationToken cancellationToken);
    /// <summary>Seed the cache with an index built during a publish, so the next route save doesn't rebuild it.</summary>
    void Store(int networkId, int version, NetworkMatchIndex index);
    void Invalidate(int networkId);
}

/// <summary>
/// Match indexes keyed by network id and version: a route save must not rebuild a 137k-sample
/// index, and a reassembly (which bumps the version) must not reuse the old one. Only completed
/// indexes are cached — never a task bound to a caller's scoped DbContext — and, like
/// <see cref="MatcherIndexCache"/>, entries idle for half an hour are dropped: the work is
/// bursty (an assembly, then nothing for days) and resident indexes cost tens of MB each on the NAS.
/// </summary>
public sealed class NetworkMatchIndexCache : INetworkMatchIndexCache, IDisposable
{
    private static readonly TimeSpan IdleTimeout = TimeSpan.FromMinutes(30);

    private sealed class Entry
    {
        public int Version;
        public NetworkMatchIndex Index;
        public DateTime LastUsedUtc;
    }

    private readonly ConcurrentDictionary<int, Entry> _entries = new();
    private readonly Timer _sweep;

    public NetworkMatchIndexCache()
    {
        _sweep = new Timer(_ => DropIdle(), null, IdleTimeout, IdleTimeout);
    }

    public async Task<NetworkMatchIndex> GetAsync(OVDBDatabaseContext db, RegionNetwork network, CancellationToken cancellationToken)
    {
        if (_entries.TryGetValue(network.Id, out var entry) && entry.Version == network.Version)
        {
            entry.LastUsedUtc = DateTime.UtcNow;
            return entry.Index;
        }

        // Two callers racing here both build; writes are serialised by NetworkWriteLock and
        // route saves are rare, so a duplicate build beats holding a lock across a database read.
        var index = await BuildAsync(db, network, cancellationToken);
        Store(network.Id, network.Version, index);
        return index;
    }

    public void Store(int networkId, int version, NetworkMatchIndex index) =>
        _entries[networkId] = new Entry { Version = version, Index = index, LastUsedUtc = DateTime.UtcNow };

    public void Invalidate(int networkId) => _entries.TryRemove(networkId, out _);

    private void DropIdle()
    {
        var cutoff = DateTime.UtcNow - IdleTimeout;
        foreach (var (id, entry) in _entries)
            if (entry.LastUsedUtc < cutoff)
                _entries.TryRemove(id, out _);
    }

    public void Dispose() => _sweep.Dispose();

    /// <summary>
    /// Build an index from the database as <paramref name="db"/> currently sees it. Inside a
    /// publish transaction that is the uncommitted new edges, which is exactly what the publish
    /// matches against; such an index must only be stored once the transaction committed.
    /// </summary>
    public static async Task<NetworkMatchIndex> BuildAsync(OVDBDatabaseContext db, RegionNetwork network, CancellationToken cancellationToken)
    {
        var edges = await db.NetworkEdges
            .Where(e => e.RegionNetworkId == network.Id)
            .Select(e => new MatchableEdge(e.Id, e.Geometry, e.SampleCount, e.HiddenTwins))
            .ToListAsync(cancellationToken);
        var lat0 = await db.Regions.Where(r => r.Id == network.RegionId).Select(r => r.Geometry.Centroid.Y).FirstAsync(cancellationToken);
        return new NetworkMatchIndex(edges, new NetworkGeo.LocalProjection(lat0));
    }
}

public interface INetworkMatchService
{
    /// <summary>Replace one route's match rows, atomically. The caller holds <see cref="NetworkWriteLock"/>.</summary>
    Task MatchRouteAsync(int routeId, CancellationToken cancellationToken = default);

    /// <summary>Replace all match rows of a published network, atomically. The caller holds <see cref="NetworkWriteLock"/>.</summary>
    Task RematchNetworkAsync(int regionNetworkId, Func<int, Task> progress = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// Match every eligible route of the network's region and add the rows, inside the caller's
    /// transaction (assembly publishes edges and rows as one unit). Returns the row count.
    /// </summary>
    Task<int> WriteMatchesAsync(RegionNetwork network, NetworkMatchIndex index, Func<int, Task> progress, CancellationToken cancellationToken);
}

/// <summary>
/// Writes <see cref="RouteNetworkEdge"/> rows: per route on change, per network on publish or
/// rematch. Eligibility is the OVDB-native <c>RouteType.MatchesRailNetworks</c>; a route of any
/// rail-bound type matches every published network of its regions (geometry decides what it covers).
/// Every delete-and-insert runs as one transaction through the execution strategy (the app's
/// DbContext retries on transient failures, which forbids bare user transactions), so a reader
/// never sees a route or network with its old rows gone and its new rows not yet written.
/// </summary>
public class NetworkMatchService(OVDBDatabaseContext db, INetworkMatchIndexCache indexCache, ILogger<NetworkMatchService> logger) : INetworkMatchService
{
    private const int RoutePageSize = 200;

    public async Task MatchRouteAsync(int routeId, CancellationToken cancellationToken = default)
    {
        var route = await db.Routes
            .Where(r => r.RouteId == routeId)
            .Select(r => new
            {
                r.RouteId,
                r.LineString,
                Eligible = r.RouteType != null && r.RouteType.MatchesRailNetworks,
                RegionIds = r.Regions.Select(reg => reg.Id).ToList(),
            })
            .FirstOrDefaultAsync(cancellationToken);

        // Matching is pure CPU on cached indexes; do it before opening the transaction.
        var rows = new List<RouteNetworkEdge>();
        if (route != null && route.Eligible && route.LineString != null && route.RegionIds.Count > 0)
        {
            var networks = await db.RegionNetworks
                .AsNoTracking()
                .Where(n => n.Version > 0 && route.RegionIds.Contains(n.RegionId))
                .ToListAsync(cancellationToken);
            foreach (var network in networks)
            {
                var index = await indexCache.GetAsync(db, network, cancellationToken);
                foreach (var (edgeId, bits) in index.Match(route.LineString))
                    rows.Add(ToRow(routeId, edgeId, bits));
            }
        }

        var strategy = db.Database.CreateExecutionStrategy();
        await strategy.ExecuteAsync(async () =>
        {
            db.ChangeTracker.Clear();
            await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
            await db.RouteNetworkEdges.Where(m => m.RouteId == routeId).ExecuteDeleteAsync(cancellationToken);
            if (rows.Count > 0)
            {
                // Fresh entities per attempt: a retried attempt must not see the previous one's state.
                db.RouteNetworkEdges.AddRange(rows.Select(Clone));
                await db.SaveChangesAsync(cancellationToken);
            }
            await transaction.CommitAsync(cancellationToken);
        });
        db.ChangeTracker.Clear();
    }

    public async Task RematchNetworkAsync(int regionNetworkId, Func<int, Task> progress = null, CancellationToken cancellationToken = default)
    {
        var network = await db.RegionNetworks.AsNoTracking().FirstOrDefaultAsync(n => n.Id == regionNetworkId, cancellationToken);
        if (network == null || network.Version == 0)
            return;

        indexCache.Invalidate(network.Id);
        var index = await indexCache.GetAsync(db, network, cancellationToken);

        var matched = 0;
        var strategy = db.Database.CreateExecutionStrategy();
        await strategy.ExecuteAsync(async () =>
        {
            db.ChangeTracker.Clear();
            await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
            await db.RouteNetworkEdges.Where(m => m.Edge.RegionNetworkId == network.Id).ExecuteDeleteAsync(cancellationToken);
            // 100 means "committed, reload now": never report it from inside the transaction.
            matched = await WriteMatchesAsync(network, index, progress == null ? null : p => progress(Math.Min(p, 99)), cancellationToken);
            await transaction.CommitAsync(cancellationToken);
        });
        db.ChangeTracker.Clear();
        if (progress != null)
            await progress(100);
        logger.LogInformation("Rematched network {NetworkId} ({Mode}): {Rows} match rows", network.Id, network.Mode, matched);
    }

    public async Task<int> WriteMatchesAsync(RegionNetwork network, NetworkMatchIndex index, Func<int, Task> progress, CancellationToken cancellationToken)
    {
        var routeIds = await db.Routes
            .Where(r => r.Regions.Any(reg => reg.Id == network.RegionId) && r.RouteType != null && r.RouteType.MatchesRailNetworks && r.LineString != null)
            .OrderBy(r => r.RouteId)
            .Select(r => r.RouteId)
            .ToListAsync(cancellationToken);

        var done = 0;
        var matched = 0;
        foreach (var page in routeIds.Chunk(RoutePageSize))
        {
            // Geometry is paged: all routes at once is hundreds of MB (see MatcherIndex).
            var routes = await db.Routes
                .Where(r => page.Contains(r.RouteId))
                .Select(r => new { r.RouteId, r.LineString })
                .ToListAsync(cancellationToken);
            var rows = new List<RouteNetworkEdge>();
            foreach (var route in routes)
            {
                foreach (var (edgeId, bits) in index.Match(route.LineString))
                    rows.Add(ToRow(route.RouteId, edgeId, bits));
            }
            if (rows.Count > 0)
            {
                db.RouteNetworkEdges.AddRange(rows);
                await db.SaveChangesAsync(cancellationToken);
                db.ChangeTracker.Clear();
                matched += rows.Count;
            }
            done += page.Length;
            if (progress != null)
                await progress(done * 100 / routeIds.Count);
        }
        if (routeIds.Count == 0 && progress != null)
            await progress(100);
        logger.LogInformation("Matched {Routes} routes against network {NetworkId} ({Mode}): {Rows} match rows", routeIds.Count, network.Id, network.Mode, matched);
        return matched;
    }

    private static RouteNetworkEdge ToRow(int routeId, long edgeId, bool[] bits) => new()
    {
        RouteId = routeId,
        NetworkEdgeId = edgeId,
        CoveredSamples = NetworkLattice.ToBytes(bits),
        CoveredCount = NetworkLattice.PopCount(bits),
    };

    private static RouteNetworkEdge Clone(RouteNetworkEdge row) => new()
    {
        RouteId = row.RouteId,
        NetworkEdgeId = row.NetworkEdgeId,
        CoveredSamples = row.CoveredSamples,
        CoveredCount = row.CoveredCount,
    };
}
