using Microsoft.EntityFrameworkCore;
using NetTopologySuite.IO;
using NetTopologySuite.Operation.Overlay;
using NetTopologySuite.Operation.OverlayNG;
using OVDB_database.Database;
using OVDB_database.Models;
using System;
using System.Collections.Generic;
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
        if (route.Regions == null)
            route.Regions = [];
        else
            route.Regions.Clear();
        var applicableRegions = await dbContext.Regions.Where(r => !r.ParentRegionId.HasValue).Where(r => r.Geometry.Intersects(route.LineString)).ToListAsync();

        foreach (var region in applicableRegions)
        {
            route.Regions.Add(region);
        }

        var usedRegions = applicableRegions.Select(r => r.Id).ToList();

        var intermediateRegions = (await dbContext.Regions.Where(r => r.ParentRegionId.HasValue && usedRegions.Contains(r.ParentRegionId.Value)).ToListAsync());

        var isValidOp = new NetTopologySuite.Operation.Valid.IsValidOp(route.LineString);

        foreach (var region in intermediateRegions)
        {
            var intersection = OverlayNG.Overlay(region.Geometry, route.LineString, SpatialFunction.Intersection);
            if (!intersection.IsEmpty)
                route.Regions.Add(region);
        }

        // Process intermediate regions
        var subRegions = (await dbContext.Regions.Where(r => r.ParentRegionId.HasValue && intermediateRegions.Select(sr => sr.Id).Contains(r.ParentRegionId.Value)).ToListAsync());

        foreach (var region in subRegions.Where(ir=>ir.Geometry!=null))
        {
            try
            {
                var intersection = OverlayNG.Overlay(region.Geometry, route.LineString, SpatialFunction.Intersection);
                if (!intersection.IsEmpty)
                    route.Regions.Add(region);
            }
            catch
            {
                Console.WriteLine("Unable to check " + region.Name);
            }
        }
    }
}
