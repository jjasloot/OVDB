using GeoJSON.Net.Feature;
using GeoJSON.Net.Geometry;
using Microsoft.AspNet.OData;
using Microsoft.AspNet.OData.Query;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.OData.Edm;
using Microsoft.OData.UriParser;
using OVDB_database.Database;
using OVDB_database.Models;
using SharpKml.Base;
using SharpKml.Dom;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;

namespace OV_DB.Controllers
{
    [Route("odata")]
    [AllowAnonymous]
    public class RoutesOdataController : ODataController
    {
        private readonly OVDBDatabaseContext _context;

        public RoutesOdataController(OVDBDatabaseContext context)
        {
            _context = context;
        }

        [HttpGet("{id}")]
        [Produces("application/json")]
        public async Task<ActionResult<FeatureCollection>> GetGeoJsonAsync(string id, ODataQueryOptions<Route> q, [FromQuery] string language)
        {
            var userClaim = User.Claims.SingleOrDefault(c => c.Type == ClaimTypes.NameIdentifier);
            var userIdClaim = int.Parse(userClaim != null ? userClaim.Value : "-1");

            var guid = Guid.Parse(id);
            var map = await _context.Maps.SingleOrDefaultAsync(m => m.MapGuid == guid);
            if (map == null)
            {
                return NotFound();
            }
            if (string.IsNullOrWhiteSpace(map.SharingLinkName))
            {
                if (User.Claims.Count() == 0)
                {
                    return Forbid();
                }
                var adminClaim = (User.Claims.SingleOrDefault(c => c.Type == "admin").Value ?? "false");

                if ((userIdClaim < 0 || map.UserId != userIdClaim) && !string.Equals(adminClaim, "true", StringComparison.OrdinalIgnoreCase))
                {
                    return Forbid();
                }
            }

            var routes = _context.Routes
                .Include(r => r.RouteType)
                .Include(r => r.RouteInstances)
                .Where(r => r.RouteTypeId.HasValue && r.RouteMaps.Any(rm => rm.MapId == map.MapId))
                .AsQueryable();

            var types = await _context.RouteTypes.ToListAsync();
            var colours = new Dictionary<int, LineStyle>();
            types.ForEach(t =>
            {
                colours.Add(t.TypeId, new LineStyle { Color = Color32.Parse(t.Colour) });
            });

            if (q.Filter != null)
            {
                var model = Startup.GetEdmModel();
                IEdmType type = model.FindDeclaredType("OVDB_database.Models.Route");
                IEdmNavigationSource source = model.FindDeclaredEntitySet("Products");
                var parser = new ODataQueryOptionParser(model, type, source, new Dictionary<string, string> { { "$filter", q.Filter.RawValue } });
                var context = new ODataQueryContext(model, typeof(Route), q.Context.Path);
                var filter = new FilterQueryOption(q.Filter.RawValue, context, parser);

                routes = q.Filter.ApplyTo(routes, new ODataQuerySettings()) as IQueryable<Route>;
            }

            var collection = new FeatureCollection();
            await routes.ForEachAsync(r =>
            {
                try
                {
                    var multi = r.Coordinates.Split("###").ToList();
                    var lines = new List<GeoJSON.Net.Geometry.LineString>();
                    multi.ForEach(block =>
                    {
                        var coordinates = block.Split('\n').Where(a => !string.IsNullOrWhiteSpace(a)).ToList();
                        var coords = coordinates.Select(r => new Position(double.Parse(r.Split(',')[1], CultureInfo.InvariantCulture), double.Parse(r.Split(',')[0], CultureInfo.InvariantCulture))).ToList();
                        if (coords.Count >= 2)
                        {
                            var geo = new GeoJSON.Net.Geometry.LineString(coords);
                            lines.Add(geo);
                        }
                    });
                    GeoJSON.Net.Feature.Feature feature;
                    if (lines.Count == 1)
                    {
                        feature = new GeoJSON.Net.Feature.Feature(lines.Single());
                    }
                    else
                    {
                        var multiLineString = new MultiLineString(lines);
                        feature = new GeoJSON.Net.Feature.Feature(multiLineString);
                    }
                    feature.Properties.Add("id", r.RouteId);
                    feature.Properties.Add("totalInstances", r.RouteInstances.Count);

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
                    if (!string.IsNullOrWhiteSpace(r.OverrideColour))
                        feature.Properties.Add("stroke", r.OverrideColour);
                    else
                        feature.Properties.Add("stroke", r.RouteType.Colour);
                    collection.Features.Add(feature);
                }
                catch (Exception ex)
                {
                    Console.WriteLine(ex.Message);
                    throw;
                }
            });

            return collection;
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

            var multi = route.Coordinates.Split("###").ToList();
            var lines = new List<GeoJSON.Net.Geometry.LineString>();
            multi.ForEach(block =>
            {
                var coordinates = block.Split('\n').Where(a => !string.IsNullOrWhiteSpace(a)).ToList();
                var coords = coordinates.Select(r => new Position(double.Parse(r.Split(',')[1], CultureInfo.InvariantCulture), double.Parse(r.Split(',')[0], CultureInfo.InvariantCulture))).ToList();
                if (coords.Count >= 2)
                {
                    var geo = new GeoJSON.Net.Geometry.LineString(coords);
                    lines.Add(geo);
                }
            });
            GeoJSON.Net.Feature.Feature feature;
            if (lines.Count == 1)
            {
                feature = new GeoJSON.Net.Feature.Feature(lines.Single());
            }
            else
            {
                var multiLineString = new MultiLineString(lines);
                feature = new GeoJSON.Net.Feature.Feature(multiLineString);
            }
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
                feature.Properties.Add("stroke", route.RouteType?.Colour??"#000");

            collection.Features.Add(feature);

            return collection;
        }
    }
}
