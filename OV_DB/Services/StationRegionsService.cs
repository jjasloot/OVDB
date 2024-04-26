using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using NetTopologySuite.Geometries;
using NetTopologySuite.Operation.Overlay;
using NetTopologySuite.Operation.OverlayNG;
using OVDB_database.Database;
using OVDB_database.Models;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace OV_DB.Services;

public interface IStationRegionsService
{
    Task AssignRegionsToStationAsync(Station station);
    Task AssignRegionsToStationCacheRegionsAsync(Station station);
}

public class StationRegionsService(OVDBDatabaseContext dbContext) : IStationRegionsService
{
    private List<Region> _regions;

    public async Task AssignRegionsToStationAsync(Station station)
    {
        NetTopologySuite.NtsGeometryServices.Instance = new NetTopologySuite.NtsGeometryServices(GeometryOverlay.NG);
        station.Regions.Clear();
        var location = new Point(station.Longitude, station.Lattitude);
        var applicableRegions = await dbContext.Regions.Where(r => r.Geometry.Intersects(location)).ToListAsync();

        foreach (var region in applicableRegions)
        {
            station.Regions.Add(region);
        }
    }

    public async Task AssignRegionsToStationCacheRegionsAsync(Station station)
    {
        if (_regions == null)
        {
            await GetAllRegionsAsync();
        }


        NetTopologySuite.NtsGeometryServices.Instance = new NetTopologySuite.NtsGeometryServices(GeometryOverlay.NG);
        station.Regions.Clear();
        var location = new Point(station.Longitude, station.Lattitude);

        foreach (var region in _regions)
        {
            if (!OverlayNG.Overlay(region.Geometry, location, SpatialFunction.Intersection).IsEmpty)
                station.Regions.Add(region);
        }
    }

    private async Task GetAllRegionsAsync()
    {
        _regions = await dbContext.Regions.ToListAsync();
    }
}
