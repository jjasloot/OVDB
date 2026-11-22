using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Moq;
using OV_DB.Controllers;
using OV_DB.Models;
using OV_DB.Services;
using OVDB_database.Database;
using OVDB_database.Models;
using Xunit;

namespace OV_DB.Tests;

/// <summary>Backend halves of the Nov 2026 UX sweep.</summary>
public class UxSweepTests
{
    private static OVDBDatabaseContext NewContext() => new(new DbContextOptionsBuilder<OVDBDatabaseContext>()
        .UseInMemoryDatabase(Guid.NewGuid().ToString())
        .Options);

    private static ControllerContext AsUser(int userId) => new()
    {
        HttpContext = new DefaultHttpContext
        {
            User = new ClaimsPrincipal(new ClaimsIdentity(new[] { new Claim(ClaimTypes.NameIdentifier, userId.ToString()) }, "test"))
        }
    };

    /// <summary>
    /// The edit-multiple map picker leaves out completed maps, so a route's link to one must survive
    /// a "set maps" that could never have selected it.
    /// </summary>
    [Fact]
    public async Task EditMultipleKeepsLinksToCompletedMaps()
    {
        var context = NewContext();
        context.Maps.AddRange(
            new Map { MapId = 1, UserId = 1, Name = "Open", MapGuid = Guid.NewGuid() },
            new Map { MapId = 2, UserId = 1, Name = "Done", MapGuid = Guid.NewGuid(), Completed = true },
            new Map { MapId = 3, UserId = 1, Name = "Other open", MapGuid = Guid.NewGuid() },
            new Map { MapId = 4, UserId = 1, Name = "Target", MapGuid = Guid.NewGuid() });
        context.Routes.Add(new Route { RouteId = 1, Name = "Route", Share = Guid.NewGuid() });
        context.RoutesMaps.AddRange(
            new RouteMap { RouteMapId = 1, RouteId = 1, MapId = 1 },
            new RouteMap { RouteMapId = 2, RouteId = 1, MapId = 2 },
            new RouteMap { RouteMapId = 3, RouteId = 1, MapId = 3 });
        await context.SaveChangesAsync();
        var controller = new RoutesController(context, Mock.Of<IRouteRegionsService>(), Mock.Of<ITimezoneService>())
        {
            ControllerContext = AsUser(1)
        };

        await controller.UpdateMultipleRoutes(new EditMultiple
        {
            RouteIds = new List<int> { 1 },
            UpdateMaps = true,
            Maps = new List<int> { 4 },
        });

        var mapIds = await context.RoutesMaps.Where(rm => rm.RouteId == 1).Select(rm => rm.MapId).OrderBy(id => id).ToListAsync();
        Assert.Equal(new[] { 2, 4 }, mapIds);
    }

    /// <summary>Station maps live in StationGroupings; the legacy StationMaps table is not what the page reads.</summary>
    [Fact]
    public async Task StationLinkResolvesFromStationGroupings()
    {
        var context = NewContext();
        var guid = Guid.NewGuid();
        context.StationGroupings.Add(new StationGrouping { Id = 1, UserId = 1, Name = "NL", NameNL = "NL", MapGuid = guid, SharingLinkName = "my-stations" });
        await context.SaveChangesAsync();
        var controller = new UserController(context) { ControllerContext = AsUser(2) };

        var result = await controller.GetGuidFromNameForStationsAsync("my-stations");

        var ok = Assert.IsType<OkObjectResult>(result.Result);
        Assert.Equal(guid, ok.Value);
    }
}
