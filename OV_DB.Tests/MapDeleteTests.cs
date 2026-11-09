using System;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using OV_DB.Controllers;
using OVDB_database.Database;
using OVDB_database.Models;
using Xunit;

namespace OV_DB.Tests;

/// <summary>
/// Every route query filters on "is on one of my maps", so a route left on no map is lost to its
/// owner for good. Deleting a map must not leave any behind.
/// </summary>
public class MapDeleteTests
{
    private static async Task<(MapsController Controller, OVDBDatabaseContext Context)> SetUpAsync(bool withSecondMap)
    {
        var context = new OVDBDatabaseContext(new DbContextOptionsBuilder<OVDBDatabaseContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options);
        context.Maps.Add(new Map { MapId = 1, UserId = 1, Name = "Doomed", MapGuid = Guid.NewGuid() });
        if (withSecondMap)
            context.Maps.Add(new Map { MapId = 2, UserId = 1, Name = "Default", MapGuid = Guid.NewGuid(), Default = true });
        context.Routes.Add(new Route { RouteId = 1, Name = "Only on the doomed map", Share = Guid.NewGuid() });
        context.RoutesMaps.Add(new RouteMap { RouteMapId = 1, RouteId = 1, MapId = 1 });
        await context.SaveChangesAsync();

        var controller = new MapsController(context)
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext
                {
                    User = new ClaimsPrincipal(new ClaimsIdentity(new[] { new Claim(ClaimTypes.NameIdentifier, "1") }, "test"))
                }
            }
        };
        return (controller, context);
    }

    [Fact]
    public async Task RoutesOnlyOnTheDeletedMapMoveToTheDefaultMap()
    {
        var (controller, context) = await SetUpAsync(withSecondMap: true);

        await controller.DeleteMap(1);

        var link = await context.RoutesMaps.SingleAsync();
        Assert.Equal(1, link.RouteId);
        Assert.Equal(2, link.MapId);
    }

    [Fact]
    public async Task TheLastMapWithRoutesIsNotDeleted()
    {
        var (controller, context) = await SetUpAsync(withSecondMap: false);

        var result = await controller.DeleteMap(1);

        Assert.IsType<ConflictObjectResult>(result.Result);
        Assert.Equal(1, await context.Maps.CountAsync());
        Assert.Equal(1, await context.RoutesMaps.CountAsync());
    }
}
