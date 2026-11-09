using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Moq;
using OV_DB.Models;
using OV_DB.Services;
using OVDB_database.Database;
using OVDB_database.Models;
using Xunit;

namespace OV_DB.Tests;

/// <summary>The one upsert behind both the single-station import and the region station update.</summary>
public class OsmStationImportTests
{
    private static OVDBDatabaseContext NewContext() => new(new DbContextOptionsBuilder<OVDBDatabaseContext>()
        .UseInMemoryDatabase(Guid.NewGuid().ToString())
        .Options);

    private static OSMStationElement Node(long id, Dictionary<string, string> tags, double lat = 52.0, double lon = 5.0) =>
        new() { Id = id, Lat = lat, Lon = lon, Tags = tags };

    [Fact]
    public async Task UpdatesAnExistingStationInsteadOfAddingAnother()
    {
        using var context = NewContext();
        context.Stations.Add(new Station { Id = 1, OsmId = 100, Name = "Old name" });
        await context.SaveChangesAsync();
        var list = new OSMStationList { Elements = [Node(100, new() { ["name"] = "New name", ["ele"] = "3.5", ["usage"] = "tourism" })] };

        var existing = await OsmStationImport.LoadExistingAsync(context, list);
        await OsmStationImport.UpsertAsync(context, existing, list.Elements[0], Mock.Of<IStationRegionsService>());
        await context.SaveChangesAsync();

        var station = await context.Stations.SingleAsync();
        Assert.Equal("New name", station.Name);
        Assert.Equal(3.5, station.Elevation);
        Assert.True(station.Special);
    }

    [Theory]
    [InlineData("", 52.0, 5.0)]
    [InlineData("Halt", 0.0, 0.0)]
    public async Task SkipsNodesWithoutANameOrPosition(string name, double lat, double lon)
    {
        using var context = NewContext();
        var list = new OSMStationList { Elements = [Node(100, new() { ["name"] = name }, lat, lon)] };

        var existing = await OsmStationImport.LoadExistingAsync(context, list);
        await OsmStationImport.UpsertAsync(context, existing, list.Elements[0], Mock.Of<IStationRegionsService>());
        await context.SaveChangesAsync();

        Assert.Empty(await context.Stations.ToListAsync());
    }
}
