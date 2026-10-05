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
    public sealed record RegionNetworkSummaryDto(int Id, NetworkMode Mode, int Version, double TotalKm, double BonusKm, int EdgeCount, DateTime? AssembledAt, bool ClassifiedByRelations, double CoveredKm);

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

    /// <summary>Regions with at least one Ready network, with per-mode totals and the caller's covered km. Drives the picker.</summary>
    [HttpGet("regions")]
    public async Task<ActionResult<List<RegionNetworksDto>>> RegionsAsync()
    {
        var userId = User.GetUserId();
        var networks = await dbContext.RegionNetworks
            .Where(n => n.Status == NetworkStatus.Ready && n.EdgeCount > 0)
            .Select(n => new { n.Id, n.RegionId, n.Region.Name, n.Region.NameNL, n.Region.ParentRegionId, n.Mode, n.Version, n.TotalKm, n.BonusKm, n.EdgeCount, n.AssembledAt, n.ClassifiedByRelations })
            .ToListAsync();
        if (networks.Count == 0)
            return new List<RegionNetworksDto>();

        // Covered km per network for this user: union bitmaps per edge, so an edge two routes
        // share counts once. Grouped in memory - the rows are small (≤ 51 bytes each).
        var networkIds = networks.Select(n => n.Id).ToList();
        var rows = await OwnedMatchRows(userId, null, null)
            .Where(m => networkIds.Contains(m.Edge.RegionNetworkId) && m.Edge.IsPassenger)
            .Select(m => new { m.Edge.RegionNetworkId, m.NetworkEdgeId, m.CoveredSamples, m.Edge.SampleCount, m.Edge.LengthKm })
            .ToListAsync();
        var coveredByNetwork = rows
            .GroupBy(r => r.RegionNetworkId)
            .ToDictionary(g => g.Key, g => g.GroupBy(r => r.NetworkEdgeId).Sum(e =>
            {
                var union = new bool[e.First().SampleCount];
                foreach (var row in e)
                    NetworkLattice.OrInto(union, row.CoveredSamples);
                return e.First().LengthKm * NetworkLattice.PopCount(union) / union.Length;
            }));

        return networks
            .GroupBy(n => new { n.RegionId, n.Name, n.NameNL, n.ParentRegionId })
            .OrderBy(g => g.Key.Name)
            .Select(g => new RegionNetworksDto(g.Key.RegionId, g.Key.Name, g.Key.NameNL, g.Key.ParentRegionId,
                g.OrderBy(n => n.Mode).Select(n => new RegionNetworkSummaryDto(n.Id, n.Mode, n.Version, n.TotalKm, n.BonusKm, n.EdgeCount,
                    n.AssembledAt, n.ClassifiedByRelations, Math.Round(coveredByNetwork.GetValueOrDefault(n.Id), 1))).ToList()))
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
