using System;
using System.IO;
using System.Linq;
using System.Security.Claims;
using System.Text;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Primitives;
using Moq;
using OV_DB.Controllers;
using OV_DB.Services;
using OVDB_database.Database;
using OVDB_database.Models;
using Xunit;

namespace OV_DB.Tests;

/// <summary>
/// A KML file is saved in one go: maps and types named in it are created once, however many
/// placemarks use them.
/// </summary>
public class KmlImportTests
{
    private static string Placemark(string name) => $"""
        <Placemark><name>{name}</name>
          <ExtendedData><Data name="type"><value>Tram</value></Data><Data name="maps"><value>Trips</value></Data></ExtendedData>
          <LineString><coordinates>8.40,48.99,0 8.45,48.98,0</coordinates></LineString>
        </Placemark>
        """;

    [Fact]
    public async Task NewMapsAndTypesNamedTwiceAreCreatedOnce()
    {
        var kml = $"""<?xml version="1.0" encoding="UTF-8"?><kml xmlns="http://www.opengis.net/kml/2.2"><Document>{Placemark("A")}{Placemark("B")}</Document></kml>""";
        using var context = new OVDBDatabaseContext(new DbContextOptionsBuilder<OVDBDatabaseContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);
        context.Maps.Add(new Map { MapId = 1, UserId = 1, Name = "Default", MapGuid = Guid.NewGuid(), Default = true });
        await context.SaveChangesAsync();

        var bytes = Encoding.UTF8.GetBytes(kml);
        var file = new FormFile(new MemoryStream(bytes), 0, bytes.Length, "files", "two.kml");
        var http = new DefaultHttpContext
        {
            User = new ClaimsPrincipal(new ClaimsIdentity([new Claim(ClaimTypes.NameIdentifier, "1")], "test"))
        };
        http.Request.ContentType = "multipart/form-data; boundary=x";
        http.Request.Form = new FormCollection(new System.Collections.Generic.Dictionary<string, StringValues>(), new FormFileCollection { file });
        var controller = new RoutesController(context, Mock.Of<IRouteRegionsService>(), Mock.Of<ITimezoneService>())
        {
            ControllerContext = new ControllerContext { HttpContext = http }
        };

        var result = Assert.IsType<OkObjectResult>((await controller.PostKMLFiles(NullLogger<RoutesController>.Instance)).Result);

        Assert.Equal(2, Assert.IsAssignableFrom<System.Collections.IEnumerable>(result.Value).Cast<object>().Count());
        Assert.Equal(2, await context.Routes.CountAsync());
        Assert.Equal(1, await context.Maps.CountAsync(m => m.Name == "Trips"));
        Assert.Equal(1, await context.RouteTypes.CountAsync(t => t.Name == "Tram"));
    }
}
