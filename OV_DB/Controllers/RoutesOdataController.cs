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
using SharpKml.Base;
using SharpKml.Dom;
using System;
using System.Collections.Generic;
using System.Diagnostics;
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
        public async Task<ActionResult<MapDataDTO>> GetGeoJsonAsync(string id, ODataQueryOptions<RouteInstance> q, [FromQuery] string language, [FromQuery] bool includeLineColours, [FromQuery] bool limitToSelectedArea = false, [FromQuery] Guid? requestIdentifier = null, CancellationToken cancellationToken = default)
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
                .Include(ri => ri.Route)
                .ThenInclude(r => r.RouteType)
                .AsQueryable();

            var types = await _context.RouteTypes.ToListAsync(cancellationToken: cancellationToken);
            var colours = new Dictionary<int, LineStyle>();
            types.ForEach(t =>
            {
                colours.Add(t.TypeId, new LineStyle { Color = Color32.Parse(t.Colour) });
            });
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
            var routesList = new List<Route>();
            var routesToReturn = new Dictionary<int, int>();
            await routes.ForEachAsync(r =>
            {
                if (routesToReturn.TryAdd(r.RouteId, 0))
                {
                    routesList.Add(r.Route);
                }
                routesToReturn[r.RouteId] += 1;
            }, cancellationToken: cancellationToken);

            var total = routesList.Count;
            var processed = 0;

            foreach (var r in routesList)
            {
                if (limitingArea != null)
                {
                    if (cancellationToken.IsCancellationRequested)
                    {
                        break;
                    }
                    var route = r.LineString;
                    try
                    {
                        var clippedRoute = OverlayNG.Overlay(limitingArea, route, SpatialFunction.Intersection);
                        if (clippedRoute.IsEmpty)
                        {
                            continue;
                        }
                        var numGeometries = clippedRoute.NumGeometries;
                        for (var i = 0; i < numGeometries; i++)
                        {

                            var geometry = clippedRoute.GetGeometryN(i);

                            if (geometry is NetTopologySuite.Geometries.LineString lineString)
                            {
                                AddLineToCollection(language, includeLineColours, r, lineString, userIdClaim, map, collection, routesToReturn);
                            }
                            else if (geometry is NetTopologySuite.Geometries.Point _)
                            {
                                continue;
                            }
                            else
                            {
                                throw
                                new NotImplementedException(geometry.GeometryType);
                            }
                        }
                    }
                    catch (NetTopologySuite.Geometries.TopologyException ex)
                    {
                        Console.WriteLine(ex.Message);
                        //Ignore
                    }
                }
                else
                {
                    if (cancellationToken.IsCancellationRequested)
                    {
                        break;
                    }
                    AddLineToCollection(language, includeLineColours, r, r.LineString, userIdClaim, map, collection, routesToReturn);
                }
                processed++;
                if (processed % 10 == 0 && requestIdentifier != null)
                    await _mapGenerationHubContext.Clients.All.SendAsync(MapGenerationHub.GenerationUpdateMethod, requestIdentifier.Value, (int)Math.Round((100.0 * processed) / total, 0), cancellationToken: cancellationToken);
            };

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

        private static void AddLineToCollection(string language, bool includeLineColours, Route r, NetTopologySuite.Geometries.LineString lineString, int userIdClaim, Map map, FeatureCollection collection, Dictionary<int, int> routesToReturn)
        {
            try
            {

                var feature = new GeoJSON.Net.Feature.Feature(new GeoJSON.Net.Geometry.LineString(lineString.Coordinates.Select(loc => new Position(loc.Y, loc.X))));


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
                if (language == "nl" && !string.IsNullOrWhiteSpace(r.NameNL))
                    feature.Properties.Add("name", r.NameNL);
                else
                    feature.Properties.Add("name", r.Name);
                if (language == "nl" && !string.IsNullOrWhiteSpace(r.RouteType.NameNL))
                    feature.Properties.Add("type", r.RouteType.NameNL);
                else
                    feature.Properties.Add("type", r.RouteType.Name);
                if (language == "nl" && !string.IsNullOrWhiteSpace(r.DescriptionNL))
                    feature.Properties.Add("description", r.DescriptionNL);
                else
                    feature.Properties.Add("description", r.Description);
                feature.Properties.Add("lineNumber", r.LineNumber);
                feature.Properties.Add("operatingCompany", r.OperatingCompany);
                if (r.OverrideDistance.HasValue)
                {
                    feature.Properties.Add("distance", r.OverrideDistance);
                }
                else
                {
                    feature.Properties.Add("distance", r.CalculatedDistance);
                }
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
            var route = await _context.Routes.Include(r => r.RouteType).Include(r => r.RouteCountries).SingleOrDefaultAsync(r =>
                    r.RouteId == id &&
                    r.Share == guid);
            if (route == null)
            {
                return NotFound();
            }


            var collection = new FeatureCollection();

            var feature = new GeoJSON.Net.Feature.Feature(new GeoJSON.Net.Geometry.LineString(route.LineString.Coordinates.Select(loc => new Position(loc.Y, loc.X))));

            if (language == "nl" && !string.IsNullOrWhiteSpace(route.NameNL))
                feature.Properties.Add("name", route.NameNL);
            else
                feature.Properties.Add("name", route.Name);
            if (language == "nl" && !string.IsNullOrWhiteSpace(route.RouteType.NameNL))
                feature.Properties.Add("type", route.RouteType?.NameNL);
            else
                feature.Properties.Add("type", route.RouteType?.Name);
            if (language == "nl" && !string.IsNullOrWhiteSpace(route.DescriptionNL))
                feature.Properties.Add("description", route.DescriptionNL);
            else
                feature.Properties.Add("description", route.Description ?? "Unknown");
            feature.Properties.Add("lineNumber", route.LineNumber);
            feature.Properties.Add("operatingCompany", route.OperatingCompany); ;
            if (!string.IsNullOrWhiteSpace(route.OverrideColour))
                feature.Properties.Add("stroke", route.OverrideColour);
            else
                feature.Properties.Add("stroke", route.RouteType?.Colour ?? "#000");

            collection.Features.Add(feature);

            return collection;
        }
    }
}
