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

/// <summary>
/// PUT api/routes/instances takes map ids from the request body. Only the caller's own maps may
/// end up on the trip, for new and existing trips alike.
/// </summary>
public class RouteInstanceUpdateOwnershipTests
{
    private const int Me = 1;
    private const int MyMap = 10;
    private const int TheirMap = 20;

    private static async Task<(RoutesController Controller, OVDBDatabaseContext Context)> SetUpAsync()
    {
        var context = new OVDBDatabaseContext(new DbContextOptionsBuilder<OVDBDatabaseContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options);
        context.Maps.Add(new Map { MapId = MyMap, UserId = Me, Name = "Mine", MapGuid = Guid.NewGuid() });
        context.Maps.Add(new Map { MapId = TheirMap, UserId = 2, Name = "Theirs", MapGuid = Guid.NewGuid() });
        context.Routes.Add(new Route { RouteId = 1, Name = "My line", Share = Guid.NewGuid() });
        context.RoutesMaps.Add(new RouteMap { RouteMapId = 1, RouteId = 1, MapId = MyMap });
        await context.SaveChangesAsync();

        var controller = new RoutesController(context, Mock.Of<IRouteRegionsService>(), Mock.Of<ITimezoneService>())
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext
                {
                    User = new ClaimsPrincipal(new ClaimsIdentity(new[] { new Claim(ClaimTypes.NameIdentifier, Me.ToString()) }, "test"))
                }
            }
        };
        return (controller, context);
    }

    private static RouteInstanceUpdate NewTrip(params int[] mapIds) => new()
    {
        RouteId = 1,
        Date = new DateTime(2026, 8, 1),
        RouteInstanceProperties = new List<RouteInstancePropertyUpdate>(),
        RouteInstanceMaps = mapIds.Select(id => new RouteInstanceMap { MapId = id }).ToList()
    };

    [Fact]
    public async Task NewTripKeepsOwnMapsAndDropsSomeoneElses()
    {
        var (controller, context) = await SetUpAsync();

        await controller.UpdateInstance(NewTrip(MyMap, TheirMap), Mock.Of<IStationSuggestionService>());

        var maps = await context.Set<RouteInstanceMap>().Select(m => m.MapId).ToListAsync();
        Assert.Equal(new[] { MyMap }, maps);
    }

    [Fact]
    public async Task EditedTripCannotBeAddedToSomeoneElsesMap()
    {
        var (controller, context) = await SetUpAsync();
        context.RouteInstances.Add(new RouteInstance { RouteInstanceId = 5, RouteId = 1, Date = new DateTime(2026, 8, 1) });
        await context.SaveChangesAsync();

        var update = NewTrip(TheirMap);
        update.RouteInstanceId = 5;
        await controller.UpdateInstance(update, Mock.Of<IStationSuggestionService>());

        Assert.Empty(await context.Set<RouteInstanceMap>().ToListAsync());
    }

    [Fact]
    public async Task SameCheckInIsNotImportedTwice()
    {
        var (controller, context) = await SetUpAsync();
        // Seeded rather than imported: the import path's inbox cleanup uses ExecuteDelete, which
        // the in-memory provider does not support.
        context.RouteInstances.Add(new RouteInstance { RouteInstanceId = 5, RouteId = 1, Date = new DateTime(2026, 8, 1), TrawellingStatusId = 777 });
        await context.SaveChangesAsync();

        var second = NewTrip(MyMap);
        second.TraewellingStatusId = 777;
        var result = await controller.UpdateInstance(second, Mock.Of<IStationSuggestionService>());

        Assert.IsType<ConflictObjectResult>(result);
        Assert.Equal(1, await context.RouteInstances.CountAsync());
    }
}
