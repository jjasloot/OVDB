using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using OV_DB.Extensions;
using OV_DB.Hubs;
using OV_DB.Services.Network;
using OVDB_database.Database;
using OVDB_database.Models;

namespace OV_DB.Controllers;

/// <summary>Region rail network completion (docs/region-network-completion.md): assembly and status.</summary>
[Route("api/networks")]
[ApiController]
[Authorize]
public class NetworkCompletionController(OVDBDatabaseContext dbContext, IHubContext<MapGenerationHub> hubContext) : ControllerBase
{
    public sealed record NetworkStatusDto(int Id, NetworkMode Mode, NetworkStatus Status, DateTime? AssembledAt,
        string OsmDataTimestamp, int EdgeCount, int Version, double TotalKm, double BonusKm, bool ClassifiedByRelations, string Error);

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
}
