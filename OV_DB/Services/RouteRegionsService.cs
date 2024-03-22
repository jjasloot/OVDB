using Microsoft.EntityFrameworkCore;
using OVDB_database.Database;
using OVDB_database.Models;
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

        var subRegions = await dbContext.Regions.Where(r => r.ParentRegionId.HasValue && usedRegions.Contains(r.ParentRegionId.Value)).Where(r => r.Geometry.Intersects(route.LineString)).ToListAsync();
        foreach (var region in subRegions)
        {
            route.Regions.Add(region);
        }
    }
}
