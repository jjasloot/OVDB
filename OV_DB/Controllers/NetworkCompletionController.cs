using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Net.Http.Headers;
using NetTopologySuite.Geometries;
using OV_DB.Extensions;
using OV_DB.Hubs;
using OV_DB.Services.Network;
using OVDB_database.Database;
using OVDB_database.Models;

namespace OV_DB.Controllers;

/// <summary>
/// Region rail network completion (docs/region-network-completion.md): assembly, status and the
/// read path. A network is readable once published (Version &gt; 0), whatever its latest run is doing.
/// </summary>
[Route("api/networks")]
[ApiController]
[Authorize]
public class NetworkCompletionController(OVDBDatabaseContext dbContext, IHubContext<MapGenerationHub> hubContext) : ControllerBase
{
    /// <summary>Upper bound on ways in one override request: an edge has tens, a whole line a few hundred.</summary>
    public const int MaxOverrideWays = 2000;
    /// <summary>A reader retries this often when a publish lands between its queries.</summary>
    private const int ConsistentReadAttempts = 3;

    public sealed record NetworkStatusDto(int Id, NetworkMode Mode, NetworkStatus Status, DateTime? AssembledAt,
        string OsmDataTimestamp, int EdgeCount, int Version, double TotalKm, double BonusKm, bool ClassifiedByRelations, string Error);

    public sealed record RegionNetworksDto(int RegionId, string Name, string NameNL, int? ParentRegionId, List<RegionNetworkSummaryDto> Networks);
    public sealed record RegionNetworkSummaryDto(int Id, NetworkMode Mode, int Version, double TotalKm, double BonusKm, int EdgeCount, DateTime? AssembledAt, bool ClassifiedByRelations);

    /// <summary><see cref="Geometry"/> is GeoJSON written by <see cref="NetworkGeo.ToGeoJson"/> and embedded as-is.</summary>
    public sealed record GeometryEdgeDto(long Id, string Name, string FromStation, string ToStation, double LengthKm, bool IsPassenger, Newtonsoft.Json.Linq.JRaw Geometry);
    public sealed record GeometryDto(int Version, double TotalKm, double BonusKm, List<GeometryEdgeDto> Edges);

    public sealed record CoveredEdgeDto(long Id, List<double[]> Intervals);
    public sealed record CompletionDto(int Version, double Percent, double CoveredKm, double TotalKm, double BonusKm, List<CoveredEdgeDto> Edges);

    public sealed record MissingDto(string Name, string FromStation, string ToStation, double MissingKm, long[] EdgeIds);

    // ---- assembly (admin) --------------------------------------------------------------------

    /// <summary>Queue assembly of both networks of a region; <paramref name="refresh"/> refetches OSM instead of using the cache.</summary>
    [HttpPost("region/{regionId:int}/assemble")]
    public async Task<IActionResult> AssembleAsync(int regionId, [FromQuery] bool refresh = false)
    {
        if (!User.IsAdmin())
            return Forbid();
        if (!await dbContext.Regions.AnyAsync(r => r.Id == regionId))
            return NotFound();

        await QueueAssemblyAsync(regionId, refresh);
        return Ok();
    }

    /// <summary>Rematch every route of a region against its networks without reassembling (after a constant change, say).</summary>
    [HttpPost("region/{regionId:int}/rematch")]
    public async Task<IActionResult> RematchAsync(int regionId)
    {
        if (!User.IsAdmin())
            return Forbid();
        var networkIds = await dbContext.RegionNetworks.Where(n => n.RegionId == regionId && n.Version > 0).Select(n => n.Id).ToListAsync();
        if (networkIds.Count == 0)
            return NotFound();
        foreach (var id in networkIds)
            NetworkMatchQueueService.Queue.Enqueue(new NetworkChanged(id));
        return Ok();
    }

    /// <summary>
    /// Record the run as queued (the published data stays readable) and enqueue it. A region
    /// without network rows yet gets them when the run starts.
    /// </summary>
    private async Task QueueAssemblyAsync(int regionId, bool refresh)
    {
        await dbContext.RegionNetworks
            .Where(n => n.RegionId == regionId && n.Status != NetworkStatus.Assembling)
            .ExecuteUpdateAsync(s => s.SetProperty(n => n.Status, NetworkStatus.Queued));
        RegionNetworkAssemblyService.Enqueue(regionId, refresh);
        await hubContext.Clients.All.SendAsync(MapGenerationHub.NetworkUpdateMethod, regionId, 0);
    }

    // ---- curation (admin) --------------------------------------------------------------------

    public sealed record OverrideDto(long Id, long OsmWayId, NetworkOverrideKind Kind, DateTime CreatedAt);
    public sealed record OverrideRequest(long[] OsmWayIds, NetworkOverrideKind Kind);

    /// <summary>The OSM ways an edge was built from — what an override is keyed by.</summary>
    [HttpGet("edges/{edgeId:long}/ways")]
    public async Task<ActionResult<long[]>> EdgeWaysAsync(long edgeId)
    {
        if (!User.IsAdmin())
            return Forbid();
        var json = await dbContext.NetworkEdges.Where(e => e.Id == edgeId).Select(e => e.OsmWayIds).FirstOrDefaultAsync();
        if (json == null)
            return NotFound();
        return Newtonsoft.Json.JsonConvert.DeserializeObject<long[]>(json) ?? [];
    }

    [HttpGet("region/{regionId:int}/overrides")]
    public async Task<ActionResult<List<OverrideDto>>> OverridesAsync(int regionId)
    {
        if (!User.IsAdmin())
            return Forbid();
        return await dbContext.NetworkWayOverrides
            .Where(o => o.RegionId == regionId)
            .OrderByDescending(o => o.CreatedAt)
            // Stored as UTC; say so, or the client renders it as local time.
            .Select(o => new OverrideDto(o.Id, o.OsmWayId, o.Kind, DateTime.SpecifyKind(o.CreatedAt, DateTimeKind.Utc)))
            .ToListAsync();
    }

    /// <summary>
    /// Force ways out of the network or into a mode, then reassemble the region from the cached
    /// fetch so the change shows up. Upserts: a way already overridden gets the new kind.
    /// </summary>
    [HttpPost("region/{regionId:int}/overrides")]
    public async Task<ActionResult<List<OverrideDto>>> AddOverridesAsync(int regionId, [FromBody] OverrideRequest request)
    {
        if (!User.IsAdmin())
            return Forbid();
        if (request?.OsmWayIds == null || request.OsmWayIds.Length == 0)
            return BadRequest("No ways given.");
        if (request.OsmWayIds.Length > MaxOverrideWays)
            return BadRequest($"At most {MaxOverrideWays} ways per request.");
        if (request.OsmWayIds.Any(id => id <= 0))
            return BadRequest("OSM way ids are positive.");
        if (!System.Enum.IsDefined(request.Kind))
            return BadRequest("Unknown override kind.");
        if (!await dbContext.Regions.AnyAsync(r => r.Id == regionId))
            return NotFound();

        // One timestamp per request: the ways of one curated stretch share it, which is how the
        // UI groups them into a single entry with a single undo.
        var now = DateTime.UtcNow;
        var wayIds = request.OsmWayIds.Distinct().ToList();
        var existing = await dbContext.NetworkWayOverrides
            .Where(o => o.RegionId == regionId && wayIds.Contains(o.OsmWayId))
            .ToDictionaryAsync(o => o.OsmWayId);
        foreach (var wayId in wayIds)
        {
            if (existing.TryGetValue(wayId, out var row))
            {
                row.Kind = request.Kind;
                row.CreatedAt = now;
            }
            else
            {
                dbContext.NetworkWayOverrides.Add(new NetworkWayOverride { RegionId = regionId, OsmWayId = wayId, Kind = request.Kind, CreatedAt = now });
            }
        }
        await dbContext.SaveChangesAsync();

        await QueueAssemblyAsync(regionId, refresh: false);
        return await OverridesAsync(regionId);
    }

    public sealed record RemoveOverridesRequest(long[] OsmWayIds);

    /// <summary>Remove the overrides of several ways at once (one curated stretch) with a single reassembly.</summary>
    [HttpPost("region/{regionId:int}/overrides/remove")]
    public async Task<IActionResult> RemoveOverridesAsync(int regionId, [FromBody] RemoveOverridesRequest request)
    {
        if (!User.IsAdmin())
            return Forbid();
        if (request?.OsmWayIds == null || request.OsmWayIds.Length == 0)
            return BadRequest("No ways given.");
        if (request.OsmWayIds.Length > MaxOverrideWays)
            return BadRequest($"At most {MaxOverrideWays} ways per request.");

        var wayIds = request.OsmWayIds.Distinct().ToList();
        var removed = await dbContext.NetworkWayOverrides
            .Where(o => o.RegionId == regionId && wayIds.Contains(o.OsmWayId))
            .ExecuteDeleteAsync();
        if (removed == 0)
            return NotFound();

        await QueueAssemblyAsync(regionId, refresh: false);
        return NoContent();
    }

    [HttpDelete("region/{regionId:int}/overrides/{osmWayId:long}")]
    public async Task<IActionResult> RemoveOverrideAsync(int regionId, long osmWayId)
    {
        if (!User.IsAdmin())
            return Forbid();
        var removed = await dbContext.NetworkWayOverrides
            .Where(o => o.RegionId == regionId && o.OsmWayId == osmWayId)
            .ExecuteDeleteAsync();
        if (removed == 0)
            return NotFound();

        await QueueAssemblyAsync(regionId, refresh: false);
        return NoContent();
    }

    /// <summary>Per-mode state of a region's networks. The failure text is for admins only: it can carry internals.</summary>
    [HttpGet("region/{regionId:int}/status")]
    public async Task<ActionResult<NetworkStatusDto[]>> StatusAsync(int regionId)
    {
        var isAdmin = User.IsAdmin();
        var rows = await dbContext.RegionNetworks
            .Where(n => n.RegionId == regionId)
            .OrderBy(n => n.Mode)
            .Select(n => new NetworkStatusDto(n.Id, n.Mode, n.Status, n.AssembledAt, n.OsmDataTimestamp, n.EdgeCount,
                n.Version, n.TotalKm, n.BonusKm, n.ClassifiedByRelations, isAdmin ? n.Error : null))
            .ToListAsync();
        return rows.ToArray();
    }

    // ---- read path -------------------------------------------------------------------------

    /// <summary>
    /// Regions with at least one published, non-empty network and their per-mode totals. Drives
    /// the picker. Deliberately user-independent: the caller's covered km come from the
    /// completion call for the selected network, not from scanning every match row the user has.
    /// </summary>
    [HttpGet("regions")]
    public async Task<ActionResult<List<RegionNetworksDto>>> RegionsAsync()
    {
        var networks = await dbContext.RegionNetworks
            .Where(n => n.Version > 0 && n.EdgeCount > 0)
            .Select(n => new { n.Id, n.RegionId, n.Region.Name, n.Region.NameNL, n.Region.ParentRegionId, n.Mode, n.Version, n.TotalKm, n.BonusKm, n.EdgeCount, n.AssembledAt, n.ClassifiedByRelations })
            .ToListAsync();

        return networks
            .GroupBy(n => new { n.RegionId, n.Name, n.NameNL, n.ParentRegionId })
            .OrderBy(g => g.Key.Name)
            .Select(g => new RegionNetworksDto(g.Key.RegionId, g.Key.Name, g.Key.NameNL, g.Key.ParentRegionId,
                g.OrderBy(n => n.Mode).Select(n => new RegionNetworkSummaryDto(n.Id, n.Mode, n.Version, n.TotalKm, n.BonusKm, n.EdgeCount,
                    n.AssembledAt, n.ClassifiedByRelations)).ToList()))
            .ToList();
    }

    /// <summary>The static network: simplified geometry per edge. User-independent; cacheable by version.</summary>
    [HttpGet("{networkId:int}/geometry")]
    public async Task<ActionResult<GeometryDto>> GeometryAsync(int networkId)
    {
        for (var attempt = 1; ; attempt++)
        {
            var network = await dbContext.RegionNetworks.AsNoTracking().FirstOrDefaultAsync(n => n.Id == networkId && n.Version > 0);
            if (network == null)
                return NotFound();

            // A 304 must repeat the validators of the 200 it stands for.
            var etag = new EntityTagHeaderValue($"\"{network.Version}\"");
            Response.Headers.ETag = etag.ToString();
            Response.Headers.CacheControl = "private, no-cache";
            if (IfNoneMatchHits(etag))
                return StatusCode(StatusCodes.Status304NotModified);

            var rows = await dbContext.NetworkEdges
                .Where(e => e.RegionNetworkId == networkId)
                .Select(e => new { e.Id, e.Name, e.FromStation, e.ToStation, e.LengthKm, e.IsPassenger, e.SimplifiedGeometry })
                .ToListAsync();
            var edges = rows
                .Select(e => new GeometryEdgeDto(e.Id, e.Name, e.FromStation, e.ToStation, e.LengthKm, e.IsPassenger,
                    new Newtonsoft.Json.Linq.JRaw(NetworkGeo.ToGeoJson(e.SimplifiedGeometry))))
                .ToList();
            if (attempt < ConsistentReadAttempts && await VersionChangedAsync(network))
                continue;
            return new GeometryDto(network.Version, network.TotalKm, network.BonusKm, edges);
        }
    }

    /// <summary>
    /// The caller's coverage of one network, exact to the sample: per covered edge the covered
    /// runs as [start, end] fractions of the SIMPLIFIED geometry's length, ready to cut.
    /// <paramref name="from"/> and <paramref name="to"/> are calendar days, both inclusive.
    /// </summary>
    [HttpGet("{networkId:int}/completion")]
    public async Task<ActionResult<CompletionDto>> CompletionAsync(int networkId, [FromQuery] DateOnly? from = null, [FromQuery] DateOnly? to = null)
    {
        var userId = User.GetUserId();
        for (var attempt = 1; ; attempt++)
        {
            var network = await dbContext.RegionNetworks.AsNoTracking().FirstOrDefaultAsync(n => n.Id == networkId && n.Version > 0);
            if (network == null)
                return NotFound();

            var rows = await OwnedMatchRows(userId, from, to)
                .Where(m => m.Edge.RegionNetworkId == networkId)
                .Select(m => new { m.NetworkEdgeId, m.CoveredSamples })
                .ToListAsync();
            var edgeIds = rows.Select(r => r.NetworkEdgeId).Distinct().ToList();
            var edges = await dbContext.NetworkEdges
                .Where(e => edgeIds.Contains(e.Id))
                .Select(e => new { e.Id, e.SampleCount, e.LengthKm, e.IsPassenger, e.Geometry, e.SimplifiedGeometry })
                .ToDictionaryAsync(e => e.Id);

            // A publish between the two queries replaces every edge: read again rather than mix versions.
            if (attempt < ConsistentReadAttempts && await VersionChangedAsync(network))
                continue;

            double coveredKm = 0, bonusKm = 0;
            var covered = new List<CoveredEdgeDto>(edgeIds.Count);
            foreach (var group in rows.GroupBy(r => r.NetworkEdgeId))
            {
                if (!edges.TryGetValue(group.Key, out var edge))
                    continue;
                var union = new bool[edge.SampleCount];
                foreach (var row in group)
                    NetworkLattice.OrInto(union, row.CoveredSamples);
                var km = edge.LengthKm * NetworkLattice.PopCount(union) / union.Length;
                if (edge.IsPassenger) coveredKm += km; else bonusKm += km;

                var remap = new SimplifiedChainage(edge.Geometry.Coordinates, edge.SimplifiedGeometry.Coordinates);
                var intervals = NetworkLattice.RunsToIntervals(union)
                    .Select(iv => new[] { Math.Round(remap.ToSimplifiedFraction(iv[0]), 3), Math.Round(remap.ToSimplifiedFraction(iv[1]), 3) })
                    .ToList();
                covered.Add(new CoveredEdgeDto(edge.Id, intervals));
            }

            var percent = network.TotalKm <= 0 ? 0 : Math.Min(100, coveredKm / network.TotalKm * 100);
            return new CompletionDto(network.Version, Math.Round(percent, 1), Math.Round(coveredKm, 1), network.TotalKm, Math.Round(bonusKm, 1), covered);
        }
    }

    /// <summary>Uncovered passenger track as connected stretches per line name and station span: what to ride next.</summary>
    [HttpGet("{networkId:int}/missing")]
    public async Task<ActionResult<List<MissingDto>>> MissingAsync(int networkId, [FromQuery] DateOnly? from = null, [FromQuery] DateOnly? to = null, [FromQuery] double minKm = 0.5)
    {
        var userId = User.GetUserId();
        for (var attempt = 1; ; attempt++)
        {
            var network = await dbContext.RegionNetworks.AsNoTracking().FirstOrDefaultAsync(n => n.Id == networkId && n.Version > 0);
            if (network == null)
                return NotFound();

            var edges = await dbContext.NetworkEdges
                .Where(e => e.RegionNetworkId == networkId && e.IsPassenger)
                .Select(e => new { e.Id, e.Name, e.FromStation, e.ToStation, e.SampleCount, e.LengthKm, e.SimplifiedGeometry })
                .ToListAsync();
            var rows = await OwnedMatchRows(userId, from, to)
                .Where(m => m.Edge.RegionNetworkId == networkId && m.Edge.IsPassenger)
                .Select(m => new { m.NetworkEdgeId, m.CoveredSamples })
                .ToListAsync();
            if (attempt < ConsistentReadAttempts && await VersionChangedAsync(network))
                continue;

            var sampleCounts = edges.ToDictionary(e => e.Id, e => e.SampleCount);
            var coveredFraction = new Dictionary<long, double>();
            foreach (var group in rows.GroupBy(r => r.NetworkEdgeId))
            {
                if (!sampleCounts.TryGetValue(group.Key, out var count))
                    continue;
                var union = new bool[count];
                foreach (var row in group)
                    NetworkLattice.OrInto(union, row.CoveredSamples);
                coveredFraction[group.Key] = (double)NetworkLattice.PopCount(union) / count;
            }

            var uncovered = edges
                .Select(e => new MissingEdge(e.Id, e.Name, e.FromStation, e.ToStation,
                    e.LengthKm * (1 - coveredFraction.GetValueOrDefault(e.Id)),
                    e.SimplifiedGeometry.StartPoint.Coordinate, e.SimplifiedGeometry.EndPoint.Coordinate))
                .Where(e => e.MissingKm >= 0.05)
                .ToList();
            return MissingRuns.Group(uncovered, minKm)
                .Select(g => new MissingDto(g.Name, g.FromStation, g.ToStation, g.MissingKm, g.EdgeIds))
                .ToList();
        }
    }

    private async Task<bool> VersionChangedAsync(RegionNetwork network) =>
        await dbContext.RegionNetworks.Where(n => n.Id == network.Id).Select(n => n.Version).FirstOrDefaultAsync() != network.Version;

    /// <summary>RFC 9110 If-None-Match: a list, weak comparison, and "*".</summary>
    private bool IfNoneMatchHits(EntityTagHeaderValue etag)
    {
        var header = Request.Headers.IfNoneMatch;
        if (header.Count == 0 || !EntityTagHeaderValue.TryParseList(header, out var tags))
            return false;
        return tags.Any(t => t.Equals(EntityTagHeaderValue.Any) || t.Compare(etag, useStrongComparison: false));
    }

    /// <summary>
    /// Match rows of routes the caller owns and has actually travelled (an instance in the
    /// optional day range). Instance dates are calendar days, so the range is compared as days:
    /// <paramref name="to"/> includes the whole of that day.
    /// </summary>
    private IQueryable<RouteNetworkEdge> OwnedMatchRows(int userId, DateOnly? from, DateOnly? to)
    {
        var fromTime = from?.ToDateTime(TimeOnly.MinValue);
        var toExclusive = to?.AddDays(1).ToDateTime(TimeOnly.MinValue);
        return dbContext.RouteNetworkEdges
            .Where(m => m.Route.RouteMaps.Any(rm => rm.Map.UserId == userId))
            .Where(m => m.Route.RouteInstances.Any(ri =>
                (fromTime == null || ri.Date >= fromTime) && (toExclusive == null || ri.Date < toExclusive)));
    }
}
