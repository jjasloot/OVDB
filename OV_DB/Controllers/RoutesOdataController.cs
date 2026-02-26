using GeoJSON.Net.Feature;
using GeoJSON.Net.Geometry;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.OData.Query;
using Microsoft.AspNetCore.OData.Routing.Controllers;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using Microsoft.OData.Edm;
using Microsoft.OData.UriParser;
using NetTopologySuite.Operation.Overlay;
using NetTopologySuite.Operation.OverlayNG;
using NetTopologySuite.Operation.Union;
using OV_DB.Hubs;
using OV_DB.Models;
using OVDB_database.Database;
using OVDB_database.Models;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

namespace OV_DB.Controllers
{
    [Route("odata")]
    [AllowAnonymous]
    public class RoutesOdataController : ODataController
    {
        private readonly OVDBDatabaseContext _context;
        private readonly IHubContext<MapGenerationHub> _mapGenerationHubContext;

        public RoutesOdataController(OVDBDatabaseContext context, IHubContext<MapGenerationHub> mapGenerationHubContext)
        {
            _context = context;
            _mapGenerationHubContext = mapGenerationHubContext;
        }

        [HttpGet("{id}")]
        [Produces("application/json")]
        public async Task<ActionResult<MapDataDTO>> GetGeoJsonAsync(string id, ODataQueryOptions<RouteInstance> q, [FromQuery] string language, [FromQuery] bool includeLineColours, [FromQuery] bool limitToSelectedArea = false, [FromQuery] Guid? requestIdentifier = null, [FromQuery] double simplificationTolerance = 0.00001, CancellationToken cancellationToken = default)
        {
            var userClaim = User.Claims.SingleOrDefault(c => c.Type == ClaimTypes.NameIdentifier);
            var userIdClaim = int.Parse(userClaim != null ? userClaim.Value : "-1");

            var guid = Guid.Parse(id);
            var map = await _context.Maps.SingleOrDefaultAsync(m => m.MapGuid == guid, cancellationToken: cancellationToken);
            if (map == null)
            {
                return NotFound();
            }
            if (string.IsNullOrWhiteSpace(map.SharingLinkName))
            {
                if (!User.Claims.Any())
                {
                    return Forbid();
                }
                var adminClaim = (User.Claims.SingleOrDefault(c => c.Type == "admin").Value ?? "false");

                if ((userIdClaim < 0 || map.UserId != userIdClaim) && !string.Equals(adminClaim, "true", StringComparison.OrdinalIgnoreCase))
                {
                    return Forbid();
                }
            }
            var routes = _context.RouteInstances
                .AsNoTracking()
                .AsQueryable();

            NetTopologySuite.Geometries.Geometry? limitingArea = null;
            if (q.Filter != null)
            {
                var model = Startup.GetEdmModel();
                IEdmType type = model.FindDeclaredType("OVDB_database.Models.RouteInstance");
                IEdmNavigationSource source = model.FindDeclaredEntitySet("Products");
                var parser = new ODataQueryOptionParser(model, type, source, new Dictionary<string, string> { { "$filter", q.Filter.RawValue } });
                var context = new ODataQueryContext(model, typeof(RouteInstance), q.Context.Path);
                var filter = new FilterQueryOption(q.Filter.RawValue, context, parser);
                routes = q.Filter.ApplyTo(routes, new ODataQuerySettings()) as IQueryable<RouteInstance>;

                if (limitToSelectedArea)
                {
                    limitingArea = await ExtractAreaFromQueryAsync(q, cancellationToken);
                }
            }
            routes = routes.Where(r => r.RouteInstanceMaps.Any(rim => rim.MapId == map.MapId) || r.Route.RouteMaps.Any(rm => rm.MapId == map.MapId));
            var collection = new FeatureCollection();
            // Two lightweight DB queries instead of streaming all instances+routes into memory
            var routesToReturn = await routes
                .GroupBy(r => r.RouteId)
                .Select(g => new { RouteId = g.Key, Count = g.Count() })
                .ToDictionaryAsync(g => g.RouteId, g => g.Count, cancellationToken);
            var routeIds = routesToReturn.Keys.ToList();
            var routesList = await _context.Routes
                .AsNoTracking()
                .Include(r => r.RouteType)
                .Where(r => routeIds.Contains(r.RouteId))
                .ToListAsync(cancellationToken);

            var total = routesList.Count;
            var processed = 0;

            // Immediately transition spinner from indeterminate to determinate
            if (requestIdentifier != null)
                await _mapGenerationHubContext.Clients.All.SendAsync(MapGenerationHub.GenerationUpdateMethod, requestIdentifier.Value, 5, cancellationToken: cancellationToken);

            if (limitingArea == null)
            {
                // Fast path: no geometry clipping needed
                foreach (var r in routesList)
                {
                    if (cancellationToken.IsCancellationRequested)
                        break;
                    AddLineToCollection(language, includeLineColours, r, r.LineString, userIdClaim, map, collection, routesToReturn);
                    processed++;
                    if (processed % 10 == 0 && requestIdentifier != null)
                        await _mapGenerationHubContext.Clients.All.SendAsync(MapGenerationHub.GenerationUpdateMethod, requestIdentifier.Value, 5 + (int)Math.Round(95.0 * processed / total, 0), cancellationToken: cancellationToken);
                }
            }
            else
            {
                // Area-limited path: clip geometry per route — parallelized because OverlayNG is CPU-bound
                var concurrentFeatures = new ConcurrentBag<GeoJSON.Net.Feature.Feature>();
                await Parallel.ForEachAsync(
                    routesList,
                    new ParallelOptions { CancellationToken = cancellationToken, MaxDegreeOfParallelism = Environment.ProcessorCount },
                    async (r, ct) =>
                    {
                        var features = ClipRouteToArea(limitingArea, r, language, includeLineColours, userIdClaim, map, routesToReturn);
                        foreach (var f in features)
                            concurrentFeatures.Add(f);
                        var current = Interlocked.Increment(ref processed);
                        if (requestIdentifier != null && current % 10 == 0)
                            await _mapGenerationHubContext.Clients.All.SendAsync(MapGenerationHub.GenerationUpdateMethod, requestIdentifier.Value, 5 + (int)Math.Round(95.0 * current / total, 0), cancellationToken: ct);
                    });
                collection.Features.AddRange(concurrentFeatures);
            }

            MapBoundsDTO? area = null;
            if (limitingArea != null)
            {
                var envelop = limitingArea.Envelope;
                if (envelop is NetTopologySuite.Geometries.Polygon bbox)
                {
                    //Convert bbox to GeoJSON
                    var coords = bbox.Coordinates.Select(c => new Position(c.Y, c.X)).ToList();
                    //Find SouthWest and NorthEast
                    var southWest = new Position(coords.Min(c => c.Latitude), coords.Min(c => c.Longitude));
                    var northEast = new Position(coords.Max(c => c.Latitude), coords.Max(c => c.Longitude));
                    area = new MapBoundsDTO
                    {
                        SouthEast = southWest,
                        NorthWest = northEast
                    };
                }

            }
            var response = new MapDataDTO
            {
                Routes = collection,
                Area = area
            };


            return response;
        }

        private static List<GeoJSON.Net.Feature.Feature> ClipRouteToArea(
            NetTopologySuite.Geometries.Geometry limitingArea,
            Route r,
            string language,
            bool includeLineColours,
            int userIdClaim,
            Map map,
            Dictionary<int, int> routesToReturn)
        {
            var result = new List<GeoJSON.Net.Feature.Feature>();
            NetTopologySuite.Geometries.Geometry clippedRoute;
            try
            {
                clippedRoute = OverlayNG.Overlay(limitingArea, r.LineString, SpatialFunction.Intersection);
            }
            catch (NetTopologySuite.Geometries.TopologyException ex)
            {
                Console.WriteLine(ex.Message);
                return result;
            }
            if (clippedRoute.IsEmpty)
                return result;
            for (var i = 0; i < clippedRoute.NumGeometries; i++)
            {
                var geometry = clippedRoute.GetGeometryN(i);
                if (geometry is NetTopologySuite.Geometries.LineString lineString)
                {
                    var feature = new GeoJSON.Net.Feature.Feature(new GeoJSON.Net.Geometry.LineString(lineString.Coordinates.Select(loc => new Position(Math.Round(loc.Y, 6), Math.Round(loc.X, 6)))));
                    AddFeatures(language, r, userIdClaim, map, routesToReturn, feature, includeLineColours);
                    result.Add(feature);
                }
                else if (geometry is not NetTopologySuite.Geometries.Point)
                {
                    throw new NotImplementedException(geometry.GeometryType);
                }
            }
            return result;
        }

        private static void AddLineToCollection(string language, bool includeLineColours, Route r, NetTopologySuite.Geometries.LineString lineString, int userIdClaim, Map map, FeatureCollection collection, Dictionary<int, int> routesToReturn)
        {
            try
            {
                var feature = new GeoJSON.Net.Feature.Feature(new GeoJSON.Net.Geometry.LineString(lineString.Coordinates.Select(loc => new Position(Math.Round(loc.Y, 6), Math.Round(loc.X, 6)))));
                AddFeatures(language, r, userIdClaim, map, routesToReturn, feature, includeLineColours);
                collection.Features.Add(feature);
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.Message);
                throw;
            }
        }

        private async Task<NetTopologySuite.Geometries.Geometry?> ExtractAreaFromQueryAsync(ODataQueryOptions<RouteInstance> q, CancellationToken cancellationToken)
        {
            var pattern = @"region/Id eq (\d+)";

            var matches = Regex.Matches(q.Filter.RawValue, pattern);
            var areaIds = new List<int>();
            foreach (Match match in matches)
            {
                if (int.TryParse(match.Groups[1].Value, out var newArea))
                {
                    areaIds.Add(newArea);
                }
            }
            if (!areaIds.Any())
            {
                return null;
            }
            var areas = await _context.Regions.Where(a => areaIds.Contains(a.Id)).Select(r => r.Geometry).ToListAsync(cancellationToken: cancellationToken);
            if (areas.Count == 1)
            {
                return areas.First();
            }

            var area = new CascadedPolygonUnion([.. areas]);
            var limitingArea = area.Union();

            return limitingArea;
        }

        private static void AddFeatures(string language, Route r, int userIdClaim, Map map, Dictionary<int, int> routesToReturn, GeoJSON.Net.Feature.Feature feature, bool includeLineColours)
        {
            feature.Properties.Add("id", r.RouteId);
            feature.Properties.Add("totalInstances", routesToReturn[r.RouteId]);

            if (map.ShowRouteOutline)
            {
                feature.Properties.Add("o", 1);
            }
            if (map.ShowRouteInfo)
            {
                var name = language == "nl" && !string.IsNullOrWhiteSpace(r.NameNL) ? r.NameNL : r.Name;
                if (!string.IsNullOrWhiteSpace(name))
                    feature.Properties.Add("name", name);

                var type = language == "nl" && !string.IsNullOrWhiteSpace(r.RouteType.NameNL) ? r.RouteType.NameNL : r.RouteType.Name;
                if (!string.IsNullOrWhiteSpace(type))
                    feature.Properties.Add("type", type);

                var description = language == "nl" && !string.IsNullOrWhiteSpace(r.DescriptionNL) ? r.DescriptionNL : r.Description;
                if (!string.IsNullOrWhiteSpace(description))
                    feature.Properties.Add("description", description);

                if (!string.IsNullOrWhiteSpace(r.LineNumber))
                    feature.Properties.Add("lineNumber", r.LineNumber);
                if (!string.IsNullOrWhiteSpace(r.OperatingCompany))
                    feature.Properties.Add("operatingCompany", r.OperatingCompany);

                var distance = r.OverrideDistance ?? r.CalculatedDistance;
                feature.Properties.Add("distance", distance);
            }
            if (map.UserId == userIdClaim)
            {
                feature.Properties.Add("owner", true);
            }
            if (!string.IsNullOrWhiteSpace(r.OverrideColour) && includeLineColours)
                feature.Properties.Add("stroke", r.OverrideColour);
            else
                feature.Properties.Add("stroke", r.RouteType.Colour);
        }

        [HttpGet("single/{id:int}/{guid:Guid}")]
        [Produces("application/json")]
        public async Task<ActionResult<FeatureCollection>> GetSingleRouteGeoJsonAsync(int id, Guid guid, [FromQuery] string language)
        {
            var route = await _context.Routes.Include(r => r.RouteType).SingleOrDefaultAsync(r =>
                    r.RouteId == id &&
                    r.Share == guid);
            if (route == null)
            {
                return NotFound();
            }


            var collection = new FeatureCollection();

            var feature = new GeoJSON.Net.Feature.Feature(new GeoJSON.Net.Geometry.LineString(route.LineString.Coordinates.Select(loc => new Position(Math.Round(loc.Y, 6), Math.Round(loc.X, 6)))));

            var name = language == "nl" && !string.IsNullOrWhiteSpace(route.NameNL) ? route.NameNL : route.Name;
            if (!string.IsNullOrWhiteSpace(name))
                feature.Properties.Add("name", name);
            var type = language == "nl" && !string.IsNullOrWhiteSpace(route.RouteType?.NameNL) ? route.RouteType.NameNL : route.RouteType?.Name;
            if (!string.IsNullOrWhiteSpace(type))
                feature.Properties.Add("type", type);
            var description = language == "nl" && !string.IsNullOrWhiteSpace(route.DescriptionNL) ? route.DescriptionNL : route.Description;
            feature.Properties.Add("description", description ?? "Unknown");
            if (!string.IsNullOrWhiteSpace(route.LineNumber))
                feature.Properties.Add("lineNumber", route.LineNumber);
            if (!string.IsNullOrWhiteSpace(route.OperatingCompany))
                feature.Properties.Add("operatingCompany", route.OperatingCompany);
            if (!string.IsNullOrWhiteSpace(route.OverrideColour))
                feature.Properties.Add("stroke", route.OverrideColour);
            else
                feature.Properties.Add("stroke", route.RouteType?.Colour ?? "#000");

            collection.Features.Add(feature);

            return collection;
        }
    }
}
