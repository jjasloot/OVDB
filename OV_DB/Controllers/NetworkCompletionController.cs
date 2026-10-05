using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using NetTopologySuite.Geometries;
using OV_DB.Extensions;
using OV_DB.Hubs;
using OV_DB.Services.Network;
using OVDB_database.Database;
using OVDB_database.Models;

namespace OV_DB.Controllers;

/// <summary>Region rail network completion (docs/region-network-completion.md): assembly, status and the read path.</summary>
[Route("api/networks")]
[ApiController]
[Authorize]
public class NetworkCompletionController(OVDBDatabaseContext dbContext, IHubContext<MapGenerationHub> hubContext) : ControllerBase
{
    public sealed record NetworkStatusDto(int Id, NetworkMode Mode, NetworkStatus Status, DateTime? AssembledAt,
        string OsmDataTimestamp, int EdgeCount, int Version, double TotalKm, double BonusKm, bool ClassifiedByRelations, string Error);

    public sealed record RegionNetworksDto(int RegionId, string Name, string NameNL, int? ParentRegionId, List<RegionNetworkSummaryDto> Networks);
    public sealed record RegionNetworkSummaryDto(int Id, NetworkMode Mode, int Version, double TotalKm, double BonusKm, int EdgeCount, DateTime? AssembledAt, bool ClassifiedByRelations);

    public sealed record GeometryEdgeDto(long Id, string Name, string FromStation, string ToStation, double LengthKm, bool IsPassenger, Geometry Geometry);
    public sealed record GeometryDto(int Version, double TotalKm, double BonusKm, List<GeometryEdgeDto> Edges);

    public sealed record CoveredEdgeDto(long Id, List<double[]> Intervals);
    public sealed record CompletionDto(double Percent, double CoveredKm, double TotalKm, double BonusKm, List<CoveredEdgeDto> Edges);

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

        await hubContext.Clients.All.SendAsync(MapGenerationHub.NetworkUpdateMethod, regionId, 0);
        RegionNetworkAssemblyService.Queue.Enqueue(new NetworkAssemblyRequest(regionId, refresh));
        return Ok();
    }

    /// <summary>Rematch every route of a region against its networks without reassembling (after a constant change, say).</summary>
    [HttpPost("region/{regionId:int}/rematch")]
    public async Task<IActionResult> RematchAsync(int regionId)
    {
        if (!User.IsAdmin())
            return Forbid();
        var networkIds = await dbContext.RegionNetworks.Where(n => n.RegionId == regionId && n.Status == NetworkStatus.Ready).Select(n => n.Id).ToListAsync();
        if (networkIds.Count == 0)
            return NotFound();
        foreach (var id in networkIds)
            NetworkMatchQueueService.Queue.Enqueue(new NetworkChanged(id));
        return Ok();
    }

    // ---- curation (admin) --------------------------------------------------------------------

    public sealed record OverrideDto(long Id, long OsmWayId, NetworkOverrideKind Kind, DateTime CreatedAt);
    public sealed record OverrideRequest(long[] OsmWayIds, NetworkOverrideKind Kind);

    /// <summary>The OSM ways an edge was built from — what an override is keyed by.</summary>
    [HttpGet("edges/{edgeId:long}/ways")]
    public async Task<ActionResult<long[]>> EdgeWaysAsync(long edgeId)
    {
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
            .Select(o => new OverrideDto(o.Id, o.OsmWayId, o.Kind, o.CreatedAt))
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
            return BadRequest();
        if (!await dbContext.Regions.AnyAsync(r => r.Id == regionId))
            return NotFound();

        var wayIds = request.OsmWayIds.Distinct().ToList();
        var existing = await dbContext.NetworkWayOverrides
            .Where(o => o.RegionId == regionId && wayIds.Contains(o.OsmWayId))
            .ToListAsync();
        foreach (var wayId in wayIds)
        {
            var row = existing.FirstOrDefault(o => o.OsmWayId == wayId);
            if (row == null)
                dbContext.NetworkWayOverrides.Add(new NetworkWayOverride { RegionId = regionId, OsmWayId = wayId, Kind = request.Kind, CreatedAt = DateTime.UtcNow });
            else
                row.Kind = request.Kind;
        }
        await dbContext.SaveChangesAsync();

        await hubContext.Clients.All.SendAsync(MapGenerationHub.NetworkUpdateMethod, regionId, 0);
        RegionNetworkAssemblyService.Queue.Enqueue(new NetworkAssemblyRequest(regionId, RefreshFromOsm: false));
        return await OverridesAsync(regionId);
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

        await hubContext.Clients.All.SendAsync(MapGenerationHub.NetworkUpdateMethod, regionId, 0);
        RegionNetworkAssemblyService.Queue.Enqueue(new NetworkAssemblyRequest(regionId, RefreshFromOsm: false));
        return NoContent();
    }

    [HttpGet("region/{regionId:int}/status")]
    public async Task<ActionResult<NetworkStatusDto[]>> StatusAsync(int regionId)
    {
        var rows = await dbContext.RegionNetworks
            .Where(n => n.RegionId == regionId)
            .OrderBy(n => n.Mode)
            .Select(n => new NetworkStatusDto(n.Id, n.Mode, n.Status, n.AssembledAt, n.OsmDataTimestamp, n.EdgeCount,
                n.Version, n.TotalKm, n.BonusKm, n.ClassifiedByRelations, n.Error))
            .ToListAsync();
        return rows.ToArray();
    }

    // ---- read path -------------------------------------------------------------------------

    /// <summary>
    /// Regions with at least one Ready network and their per-mode totals. Drives the picker.
    /// Deliberately user-independent: the caller's covered km come from the completion call for
    /// the selected network, not from scanning every match row the user has to fill a dropdown.
    /// </summary>
    [HttpGet("regions")]
    public async Task<ActionResult<List<RegionNetworksDto>>> RegionsAsync()
    {
        var networks = await dbContext.RegionNetworks
            .Where(n => n.Status == NetworkStatus.Ready && n.EdgeCount > 0)
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
        var network = await dbContext.RegionNetworks.FirstOrDefaultAsync(n => n.Id == networkId && n.Status == NetworkStatus.Ready);
        if (network == null)
            return NotFound();

        var etag = $"\"{network.Version}\"";
        if (Request.Headers.IfNoneMatch.Any(v => v == etag))
            return StatusCode(StatusCodes.Status304NotModified);
        Response.Headers.ETag = etag;
        Response.Headers.CacheControl = "private, max-age=0, must-revalidate";

        var edges = await dbContext.NetworkEdges
            .Where(e => e.RegionNetworkId == networkId)
            .Select(e => new GeometryEdgeDto(e.Id, e.Name, e.FromStation, e.ToStation, e.LengthKm, e.IsPassenger, e.SimplifiedGeometry))
            .ToListAsync();
        return new GeometryDto(network.Version, network.TotalKm, network.BonusKm, edges);
    }

    /// <summary>
    /// The caller's coverage of one network, exact to the sample: per covered edge the covered
    /// runs as [start, end] fractions of the SIMPLIFIED geometry's length, ready to cut.
    /// </summary>
    [HttpGet("{networkId:int}/completion")]
    public async Task<ActionResult<CompletionDto>> CompletionAsync(int networkId, [FromQuery] DateTime? from = null, [FromQuery] DateTime? to = null)
    {
        var userId = User.GetUserId();
        var network = await dbContext.RegionNetworks.FirstOrDefaultAsync(n => n.Id == networkId && n.Status == NetworkStatus.Ready);
        if (network == null)
            return NotFound();

        var rows = await OwnedMatchRows(userId, from, to)
            .Where(m => m.Edge.RegionNetworkId == networkId)
            .Select(m => new { m.NetworkEdgeId, m.CoveredSamples })
            .ToListAsync();
        if (rows.Count == 0)
            return new CompletionDto(0, 0, network.TotalKm, 0, new List<CoveredEdgeDto>());

        var edgeIds = rows.Select(r => r.NetworkEdgeId).Distinct().ToList();
        var edges = await dbContext.NetworkEdges
            .Where(e => edgeIds.Contains(e.Id))
            .Select(e => new { e.Id, e.SampleCount, e.LengthKm, e.IsPassenger, e.Geometry, e.SimplifiedGeometry })
            .ToDictionaryAsync(e => e.Id);

        double coveredKm = 0, bonusKm = 0;
        var covered = new List<CoveredEdgeDto>(edgeIds.Count);
        foreach (var group in rows.GroupBy(r => r.NetworkEdgeId))
        {
            var edge = edges[group.Key];
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
        return new CompletionDto(Math.Round(percent, 1), Math.Round(coveredKm, 1), network.TotalKm, Math.Round(bonusKm, 1), covered);
    }

    /// <summary>Uncovered passenger track grouped by line name and station span: what to ride next.</summary>
    [HttpGet("{networkId:int}/missing")]
    public async Task<ActionResult<List<MissingDto>>> MissingAsync(int networkId, [FromQuery] DateTime? from = null, [FromQuery] DateTime? to = null, [FromQuery] double minKm = 0.5)
    {
        var userId = User.GetUserId();
        if (!await dbContext.RegionNetworks.AnyAsync(n => n.Id == networkId && n.Status == NetworkStatus.Ready))
            return NotFound();

        var edges = await dbContext.NetworkEdges
            .Where(e => e.RegionNetworkId == networkId && e.IsPassenger)
            .Select(e => new { e.Id, e.Name, e.FromStation, e.ToStation, e.SampleCount, e.LengthKm })
            .ToListAsync();
        var rows = await OwnedMatchRows(userId, from, to)
            .Where(m => m.Edge.RegionNetworkId == networkId && m.Edge.IsPassenger)
            .Select(m => new { m.NetworkEdgeId, m.CoveredSamples })
            .ToListAsync();
        var coveredFraction = rows.GroupBy(r => r.NetworkEdgeId).ToDictionary(g => g.Key, g =>
        {
            var count = edges.First(e => e.Id == g.Key).SampleCount;
            var union = new bool[count];
            foreach (var row in g)
                NetworkLattice.OrInto(union, row.CoveredSamples);
            return (double)NetworkLattice.PopCount(union) / count;
        });

        return edges
            .Select(e => new { e, MissingKm = e.LengthKm * (1 - coveredFraction.GetValueOrDefault(e.Id)) })
            .Where(x => x.MissingKm >= 0.05)
            .GroupBy(x => new { Name = x.e.Name ?? "", x.e.FromStation, x.e.ToStation })
            .Select(g => new MissingDto(g.Key.Name, g.Key.FromStation, g.Key.ToStation, Math.Round(g.Sum(x => x.MissingKm), 2), g.Select(x => x.e.Id).ToArray()))
            .Where(m => m.MissingKm >= minKm)
            .OrderByDescending(m => m.MissingKm)
            .ToList();
    }

    /// <summary>Match rows of routes the caller owns and has actually travelled (an instance in the optional date range).</summary>
    private IQueryable<RouteNetworkEdge> OwnedMatchRows(int userId, DateTime? from, DateTime? to) =>
        dbContext.RouteNetworkEdges
            .Where(m => m.Route.RouteMaps.Any(rm => rm.Map.UserId == userId))
            .Where(m => m.Route.RouteInstances.Any(ri =>
                (from == null || ri.Date >= from) && (to == null || ri.Date <= to)));
}
