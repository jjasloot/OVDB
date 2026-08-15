using Microsoft.EntityFrameworkCore;
using NetTopologySuite.Geometries;
using NetTopologySuite.Geometries.Prepared;
using NetTopologySuite.Index.Strtree;
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
    // Spatial index of region envelopes -> (region, prepared geometry), built once per instance so a
    // batch of stations narrows to a few candidate regions and runs a cheap point-in-polygon test
    // instead of a full topological overlay against every region.
    private STRtree<(Region Region, IPreparedGeometry Prepared)> _regionIndex;

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
        await EnsureRegionIndexAsync();

        NetTopologySuite.NtsGeometryServices.Instance = new NetTopologySuite.NtsGeometryServices(GeometryOverlay.NG);
        station.Regions.Clear();
        var location = new Point(station.Longitude, station.Lattitude);

        foreach (var (region, prepared) in _regionIndex.Query(location.EnvelopeInternal))
        {
            if (prepared.Intersects(location))
                station.Regions.Add(region);
        }
    }

    private async Task EnsureRegionIndexAsync()
    {
        if (_regionIndex != null)
            return;

        var regions = await dbContext.Regions.Where(r => r.Geometry != null).ToListAsync();
        var index = new STRtree<(Region, IPreparedGeometry)>();
        foreach (var region in regions)
        {
            index.Insert(region.Geometry.EnvelopeInternal, (region, PreparedGeometryFactory.Prepare(region.Geometry)));
        }
        index.Build();
        _regionIndex = index;
    }
}
