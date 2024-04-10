using Microsoft.EntityFrameworkCore;
using NetTopologySuite.IO;
using NetTopologySuite.Operation.Overlay;
using NetTopologySuite.Operation.OverlayNG;
using OVDB_database.Database;
using OVDB_database.Models;
using System;
using System.Linq;
using System.Threading.Tasks;

namespace OV_DB.Services;

public interface IRouteRegionsService
{
    Task AssignRegionsToRouteAsync(Route route);
}

public class RouteRegionsService(OVDBDatabaseContext dbContext) : IRouteRegionsService
{
    public async Task AssignRegionsToRouteAsync(Route route)
    {
        NetTopologySuite.NtsGeometryServices.Instance = new NetTopologySuite.NtsGeometryServices(NetTopologySuite.Geometries.GeometryOverlay.NG);
        route.Regions.Clear();
        var applicableRegions = await dbContext.Regions.Where(r => !r.ParentRegionId.HasValue).Where(r => r.Geometry.Intersects(route.LineString)).ToListAsync();

        foreach (var region in applicableRegions)
        {
            route.Regions.Add(region);
        }

        var usedRegions = applicableRegions.Select(r => r.Id).ToList();

        var subRegions = (await dbContext.Regions.Where(r => r.ParentRegionId.HasValue && usedRegions.Contains(r.ParentRegionId.Value)).ToListAsync());

        foreach (var region in subRegions)
        {
            if (!OverlayNG.Overlay(region.Geometry,route.LineString,SpatialFunction.Intersection).IsEmpty)
                route.Regions.Add(region);
        }
    }
}
