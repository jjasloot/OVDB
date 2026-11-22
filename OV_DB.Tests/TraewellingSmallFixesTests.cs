using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Newtonsoft.Json;
using OV_DB.Controllers;
using OV_DB.Hubs;
using OV_DB.Models;
using OV_DB.Services;
using OVDB_database.Database;
using OVDB_database.Enums;
using OVDB_database.Models;
using Xunit;

namespace OV_DB.Tests;

public class TraewellingSmallFixesTests
{
    // No coordinates on either station, so no timezone lookup is possible.
    private static TrawellingStatus Status(string departurePlanned = "2026-08-01T10:05:00+02:00", string arrivalPlanned = "2026-08-01T10:50:00+02:00") =>
        JsonConvert.DeserializeObject<TrawellingStatus>($$"""
        {
            "id": 123456,
            "checkin": {
                "lineName": "RE 5",
                "origin": { "station": { "id": 1, "name": "Karlsruhe Hbf" }{{(departurePlanned == null ? "" : $$""", "departurePlanned": "{{departurePlanned}}" """)}} },
                "destination": { "station": { "id": 2, "name": "Stuttgart Hbf" }{{(arrivalPlanned == null ? "" : $$""", "arrivalPlanned": "{{arrivalPlanned}}" """)}} }
            }
        }
        """);

    private static OVDBDatabaseContext NewContext() => new(new DbContextOptionsBuilder<OVDBDatabaseContext>()
        .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);

    [Fact]
    public async Task WithoutCoordinatesTheStationClockTimeIsKept()
    {
        var mapper = new TraewellingTripMapper(Mock.Of<ITimezoneService>(), NullLogger<TraewellingTripMapper>.Instance);

        var trip = await mapper.MapStatusToTripDtoAsync(Status());

        // 10:05 at +02:00, not 08:05 UTC.
        Assert.Equal(new DateTime(2026, 8, 1, 10, 5, 0), trip.Transport.Origin.DepartureScheduled);
        Assert.Equal(new DateTime(2026, 8, 1, 10, 50, 0), trip.Transport.Destination.ArrivalScheduled);
    }

    [Fact]
    public async Task DisconnectForgetsThePendingCheckInsButKeepsConflicts()
    {
        using var context = NewContext();
        context.Users.Add(new User { Id = 1, Email = "a@test", Password = "x", TrawellingUsername = "old-account", TrawellingLastSweepAt = DateTime.UtcNow, TrawellingRefreshToken = "r" });
        context.TrawellingInboxStatuses.AddRange(
            new TrawellingInboxStatus { UserId = 1, TrawellingStatusId = 10, State = TrawellingInboxState.Pending, PayloadJson = "{}" },
            new TrawellingInboxStatus { UserId = 1, TrawellingStatusId = 11, State = TrawellingInboxState.ChangedAfterImport, PayloadJson = "{}" });
        await context.SaveChangesAsync();
        var controller = new TraewellingController(Mock.Of<ITrawellingService>(), Mock.Of<ITraewellingApiClient>(), context,
            NullLogger<TraewellingController>.Instance, Mock.Of<ITraewellingRateLimiter>(), Mock.Of<ITraewellingResyncQueue>())
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext { User = new ClaimsPrincipal(new ClaimsIdentity([new Claim(ClaimTypes.NameIdentifier, "1")], "test")) }
            }
        };

        Assert.IsType<OkObjectResult>(await controller.DisconnectAccount());

        var user = await context.Users.SingleAsync();
        Assert.Null(user.TrawellingUsername);
        Assert.Null(user.TrawellingLastSweepAt);
        Assert.Equal(11, (await context.TrawellingInboxStatuses.SingleAsync()).TrawellingStatusId);
    }

    [Fact]
    public async Task BackfillStopsAskingForTripsWithoutPlannedTimes()
    {
        using var context = NewContext();
        var user = new User { Id = 1, Email = "a@test", Password = "x" };
        context.Users.Add(user);
        context.Maps.Add(new Map { MapId = 1, UserId = 1, Name = "M", MapGuid = Guid.NewGuid() });
        context.Routes.Add(new Route { RouteId = 1, Name = "R", Share = Guid.NewGuid(), RouteMaps = [new RouteMap { MapId = 1 }] });
        context.RouteInstances.Add(new RouteInstance { RouteInstanceId = 1, RouteId = 1, Date = new DateTime(2026, 8, 1), StartTime = new DateTime(2026, 8, 1, 10, 7, 0), TrawellingStatusId = 123456 });
        await context.SaveChangesAsync();

        var api = new Mock<ITraewellingApiClient>();
        api.Setup(a => a.FetchStatusRawAsync(It.IsAny<User>(), new TraewellingStatusKey(123456, null), It.IsAny<bool>()))
            .ReturnsAsync(("{}", Status(departurePlanned: null, arrivalPlanned: null)));
        var service = new TrawellingService(api.Object, new TraewellingTripMapper(Mock.Of<ITimezoneService>(), NullLogger<TraewellingTripMapper>.Instance),
            Mock.Of<ITimezoneService>(), context, NullLogger<TrawellingService>.Instance, new MemoryCache(new MemoryCacheOptions()),
            Mock.Of<IHubContext<TraewellingHub>>());

        var first = await service.BackfillScheduledTimesAsync(user);
        var second = await service.BackfillScheduledTimesAsync(user);

        Assert.Equal((1, 0, 1), first);
        Assert.Equal((0, 0, 0), second);
        Assert.True((await context.RouteInstances.SingleAsync()).ScheduledTimesUnavailable);
        api.Verify(a => a.FetchStatusRawAsync(It.IsAny<User>(), new TraewellingStatusKey(123456, null), It.IsAny<bool>()), Times.Once);
    }
}
