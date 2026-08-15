using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using NetTopologySuite.Geometries.Prepared;
using OVDB_database.Database;
using OVDB_database.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace OV_DB.Services;

public interface IRouteRegionsService
{
    Task<bool> AssignRegionsToRouteAsync(Route route);
}

public class RouteRegionsService(OVDBDatabaseContext dbContext, ILogger<RouteRegionsService> logger) : IRouteRegionsService
{
    // Loaded once per service instance (i.e. once per batch refresh / per import request) so a
    // batch of N routes no longer issues 3N region queries that each materialize full polygons.
    private List<Region> _topLevelRegions;
    private ILookup<int, Region> _childrenByParent;
    private Dictionary<int, IPreparedGeometry> _prepared;

    private async Task EnsureHierarchyLoadedAsync()
    {
        if (_prepared != null)
            return;

        var allRegions = await dbContext.Regions.Where(r => r.Geometry != null).ToListAsync();
        _prepared = new Dictionary<int, IPreparedGeometry>(allRegions.Count);
        foreach (var region in allRegions)
        {
            _prepared[region.Id] = PreparedGeometryFactory.Prepare(region.Geometry);
        }
        _topLevelRegions = allRegions.Where(r => !r.ParentRegionId.HasValue).ToList();
        _childrenByParent = allRegions.Where(r => r.ParentRegionId.HasValue).ToLookup(r => r.ParentRegionId.Value);
    }

    public async Task<bool> AssignRegionsToRouteAsync(Route route)
    {
        NetTopologySuite.NtsGeometryServices.Instance = new NetTopologySuite.NtsGeometryServices(NetTopologySuite.Geometries.GeometryOverlay.NG);
        route.Regions ??= [];
        var existingRegions = route.Regions.Select(r => r.Id).ToHashSet();
        route.Regions.Clear();

        await EnsureHierarchyLoadedAsync();

        // Top-level regions (countries) that the route passes through.
        var matchedTopLevel = _topLevelRegions.Where(r => Intersects(r, route)).ToList();
        foreach (var region in matchedTopLevel)
        {
            route.Regions.Add(region);
        }

        // Intermediate regions: children of the matched top-level regions.
        var matchedIntermediate = new List<Region>();
        foreach (var parent in matchedTopLevel)
        {
            foreach (var child in _childrenByParent[parent.Id])
            {
                if (Intersects(child, route))
                {
                    route.Regions.Add(child);
                    matchedIntermediate.Add(child);
                }
            }
        }

        // Sub-regions: children of the matched intermediate regions.
        foreach (var parent in matchedIntermediate)
        {
            foreach (var child in _childrenByParent[parent.Id])
            {
                if (Intersects(child, route))
                {
                    route.Regions.Add(child);
                }
            }
        }

        var newRegions = route.Regions.Select(r => r.Id).ToHashSet();
        var updated = !existingRegions.SetEquals(newRegions);
        if (updated)
        {
            logger.LogDebug("Route {RouteName} (ID: {RouteId}) regions updated: {OldRegions} => {NewRegions}",
                route.Name, route.RouteId, string.Join(", ", existingRegions), string.Join(", ", newRegions));
        }
        return updated;
    }

    private bool Intersects(Region region, Route route)
    {
        if (!_prepared.TryGetValue(region.Id, out var prepared))
            return false;
        try
        {
            return prepared.Intersects(route.LineString);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Unable to check region {RegionName} (ID: {RegionId}) for route {RouteId}", region.Name, region.Id, route.RouteId);
            return false;
        }
    }
}
