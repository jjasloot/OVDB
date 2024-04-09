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
        route.Regions.Clear();
        var applicableRegions = await dbContext.Regions.Where(r => !r.ParentRegionId.HasValue).Where(r => r.Geometry.Intersects(route.LineString)).ToListAsync();

        foreach (var region in applicableRegions)
        {
            route.Regions.Add(region);
        }

        var usedRegions = applicableRegions.Select(r => r.Id).ToList();

        var subRegions = (await dbContext.Regions.Where(r => r.ParentRegionId.HasValue && usedRegions.Contains(r.ParentRegionId.Value)).ToListAsync());
        //var brandenburg = await dbContext.Regions.FindAsync(10);
        //var berlin = await dbContext.Regions.FindAsync(9);
        //var intersection = brandenburg.Geometry.Intersection(route.LineString);
        //var intersection2 = OverlayNG.Overlay(brandenburg.Geometry, route.LineString, SpatialFunction.Intersection);
        //var contains = false;

        //var wntWriter = new WKTWriter();
        //Console.WriteLine(wntWriter.Write(brandenburg.Geometry));
        //Console.WriteLine(wntWriter.Write(route.LineString));
        //Console.WriteLine(wntWriter.Write(intersection));
        //Console.WriteLine(wntWriter.Write(intersection2));

        foreach (var region in subRegions)
        {
            if (route.LineString.Intersects(region.Geometry))
                route.Regions.Add(region);
        }
    }
}
