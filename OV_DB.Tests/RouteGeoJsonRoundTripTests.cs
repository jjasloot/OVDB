using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Claims;
using System.Text;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Moq;
using NetTopologySuite.Geometries;
using OV_DB.Controllers;
using OV_DB.Services;
using OVDB_database.Database;
using OVDB_database.Models;
using Xunit;

namespace OV_DB.Tests;

/// <summary>
/// The GeoJSON export exists to be imported back: everything about a route and its trips has to
/// survive the trip, and importing the same file twice must not duplicate anything.
/// </summary>
public class RouteGeoJsonRoundTripTests
{
    private static OVDBDatabaseContext NewContext() => new(new DbContextOptionsBuilder<OVDBDatabaseContext>()
        .UseInMemoryDatabase(Guid.NewGuid().ToString())
        .Options);

    private static RoutesController ControllerFor(OVDBDatabaseContext context, int userId) =>
        new(context, Mock.Of<IRouteRegionsService>(), Mock.Of<ITimezoneService>())
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext
                {
                    User = new ClaimsPrincipal(new ClaimsIdentity([new Claim(ClaimTypes.NameIdentifier, userId.ToString())], "test"))
                }
            }
        };

    private static async Task<byte[]> ExportAsync(OVDBDatabaseContext context)
    {
        context.Maps.Add(new Map { MapId = 1, UserId = 1, Name = "Trains", MapGuid = Guid.NewGuid(), Default = true });
        context.Maps.Add(new Map { MapId = 2, UserId = 1, Name = "Holidays", MapGuid = Guid.NewGuid() });
        context.RouteTypes.Add(new RouteType { TypeId = 1, UserId = 1, Name = "Train", NameNL = "Trein", Colour = "#123456", IsTrain = true });
        context.Routes.Add(new Route
        {
            RouteId = 1,
            Name = "Karlsruhe - Stuttgart",
            NameNL = "Karlsruhe - Stuttgart NL",
            From = "Karlsruhe Hbf",
            To = "Stuttgart Hbf",
            LineNumber = "RE 1",
            OverrideColour = "#ff00ff",
            OverrideDistance = 80.5,
            RouteTypeId = 1,
            Share = Guid.Parse("11111111-2222-3333-4444-555555555555"),
            LineString = new LineString([new Coordinate(8.40, 48.99), new Coordinate(9.18, 48.78)]),
            RouteMaps = [new RouteMap { MapId = 1 }],
            RouteInstances =
            [
                new RouteInstance
                {
                    Date = new DateTime(2026, 8, 1),
                    StartTime = new DateTime(2026, 8, 1, 10, 5, 0),
                    EndTime = new DateTime(2026, 8, 1, 10, 50, 0),
                    ScheduledStartTime = new DateTime(2026, 8, 1, 10, 0, 0),
                    DurationHours = 0.75,
                    TrawellingStatusId = 777,
                    RouteInstanceMaps = [new RouteInstanceMap { MapId = 2 }],
                    RouteInstanceProperties = [new RouteInstanceProperty { Key = "seat", Value = "42" }],
                },
                // Two untimed trips on one day are two trips, not a duplicate.
                new RouteInstance { Date = new DateTime(2025, 3, 2) },
                new RouteInstance { Date = new DateTime(2025, 3, 2) },
            ],
        });
        await context.SaveChangesAsync();

        var controller = ControllerFor(context, 1);
        var body = new MemoryStream();
        controller.HttpContext.Response.Body = body;
        await controller.ExportRoute(1);
        Assert.Equal("application/geo+json", controller.HttpContext.Response.ContentType);
        return body.ToArray();
    }

    [Fact]
    public async Task ImportingAnExportIntoAnEmptyAccountRecreatesTheRoute()
    {
        using var source = NewContext();
        var file = await ExportAsync(source);

        using var target = NewContext();
        target.Maps.Add(new Map { MapId = 9, UserId = 5, Name = "Default", MapGuid = Guid.NewGuid(), Default = true });
        await target.SaveChangesAsync();

        await ControllerFor(target, 5).PostGeoJsonToDatabase(new MemoryStream(file), "export.geojson", 5);

        var route = await target.Routes
            .Include(r => r.RouteType)
            .Include(r => r.RouteMaps).ThenInclude(rm => rm.Map)
            .Include(r => r.RouteInstances).ThenInclude(ri => ri.RouteInstanceProperties)
            .Include(r => r.RouteInstances).ThenInclude(ri => ri.RouteInstanceMaps).ThenInclude(m => m.Map)
            .SingleAsync();
        Assert.Equal("Karlsruhe - Stuttgart", route.Name);
        Assert.Equal("RE 1", route.LineNumber);
        Assert.Equal(80.5, route.OverrideDistance);
        Assert.Equal(Guid.Parse("11111111-2222-3333-4444-555555555555"), route.Share);
        Assert.Equal(2, route.LineString.NumPoints);
        Assert.Equal(("Train", "Trein", "#123456", true), (route.RouteType.Name, route.RouteType.NameNL, route.RouteType.Colour, route.RouteType.IsTrain));
        Assert.Equal("Trains", Assert.Single(route.RouteMaps).Map.Name);
        Assert.Equal(3, route.RouteInstances.Count);
        var trip = route.RouteInstances.Single(ri => ri.TrawellingStatusId == 777);
        Assert.Equal(new DateTime(2026, 8, 1, 10, 5, 0), trip.StartTime);
        Assert.Equal(new DateTime(2026, 8, 1, 10, 0, 0), trip.ScheduledStartTime);
        Assert.Equal(0.75, trip.DurationHours);
        Assert.Equal("Holidays", Assert.Single(trip.RouteInstanceMaps).Map.Name);
        Assert.Equal(("seat", "42"), (Assert.Single(trip.RouteInstanceProperties).Key, trip.RouteInstanceProperties[0].Value));
        Assert.All(new[] { "Trains", "Holidays" }, name => Assert.Single(target.Maps.Where(m => m.UserId == 5 && m.Name == name)));
    }

    [Fact]
    public async Task ImportingTheSameFileTwiceAddsOnlyWhatIsMissing()
    {
        using var context = NewContext();
        var file = await ExportAsync(context);
        // One trip deleted since the export; the re-import must bring back just that one.
        context.RouteInstances.Remove(await context.RouteInstances.FirstAsync(ri => ri.TrawellingStatusId == null));
        await context.SaveChangesAsync();

        var controller = ControllerFor(context, 1);
        await controller.PostGeoJsonToDatabase(new MemoryStream(file), "export.geojson", 1);
        await controller.PostGeoJsonToDatabase(new MemoryStream(file), "export.geojson", 1);

        Assert.Equal(1, await context.Routes.CountAsync());
        Assert.Equal(3, await context.RouteInstances.CountAsync());
    }

    [Fact]
    public async Task APlainGeoJsonLineFromAnotherToolBecomesARoute()
    {
        // BRouter style: a LineString with elevation and unrelated properties.
        const string brouter = """
        {"type":"FeatureCollection","features":[{"type":"Feature",
          "properties":{"creator":"BRouter-1.7.7","name":"brouter_trekking_0","track-length":"1234"},
          "geometry":{"type":"LineString","coordinates":[[8.40,48.99,115],[8.45,48.98,120],[8.50,48.97,118]]}}]}
        """;
        using var context = NewContext();
        context.Maps.Add(new Map { MapId = 1, UserId = 1, Name = "Default", MapGuid = Guid.NewGuid(), Default = true });
        await context.SaveChangesAsync();

        await ControllerFor(context, 1).PostGeoJsonToDatabase(new MemoryStream(Encoding.UTF8.GetBytes(brouter)), "ride.geojson", 1);

        var route = await context.Routes.Include(r => r.RouteMaps).SingleAsync();
        Assert.Equal("brouter_trekking_0", route.Name);
        Assert.Equal(3, route.LineString.NumPoints);
        Assert.True(double.IsNaN(route.LineString.Coordinates[0].Z));
        Assert.Equal(1, Assert.Single(route.RouteMaps).MapId);
        Assert.Empty(await context.RouteInstances.ToListAsync());
    }
}
