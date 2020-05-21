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
    [Route("odata/kml")]
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
                var userIdClaim = int.Parse(User.Claims.SingleOrDefault(c => c.Type == ClaimTypes.NameIdentifier).Value ?? "-1");
                if (userIdClaim < 0 || map.UserId != userIdClaim)
                {
                    return Forbid();
                }
            }

            var routes = _context.Routes
                .Include(r => r.RouteType)
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
                feature.Properties.Add("operatingCompany", r.OperatingCompany); ;
                if (!string.IsNullOrWhiteSpace(r.OverrideColour))
                    feature.Properties.Add("stroke", r.OverrideColour);
                else
                    feature.Properties.Add("stroke", r.RouteType.Colour);

                collection.Features.Add(feature);
            });

            return collection;
        }

        //[HttpGet]
        //[Authorize]
        //[Produces("application/json")]
        //public async Task<ActionResult<FeatureCollection>> GetGeoJsonForMeAsync(ODataQueryOptions<Route> q)
        //{
        //    var userIdClaim = int.Parse(User.Claims.SingleOrDefault(c => c.Type == ClaimTypes.NameIdentifier).Value ?? "-1");
        //    if (userIdClaim < 0)
        //    {
        //        return Forbid();
        //    }

        //    var routes = _context.Routes.Include(r => r.RouteType).Where(r => r.RouteTypeId.HasValue && r.Map.UserId == userIdClaim).AsQueryable();

        //    var types = await _context.RouteTypes.ToListAsync();
        //    var colours = new Dictionary<int, LineStyle>();
        //    types.ForEach(t =>
        //    {
        //        colours.Add(t.TypeId, new LineStyle { Color = Color32.Parse(t.Colour) });
        //    });

        //    if (q.Filter != null)
        //    {
        //        var model = Startup.GetEdmModel();
        //        IEdmType type = model.FindDeclaredType("OVDB_database.Models.Route");
        //        IEdmNavigationSource source = model.FindDeclaredEntitySet("Products");
        //        var parser = new ODataQueryOptionParser(model, type, source, new Dictionary<string, string> { { "$filter", q.Filter.RawValue } });
        //        var context = new ODataQueryContext(model, typeof(Route), q.Context.Path);
        //        var filter = new FilterQueryOption(q.Filter.RawValue, context, parser);

        //        routes = q.Filter.ApplyTo(routes, new ODataQuerySettings()) as IQueryable<Route>;
        //    }

        //    var collection = new FeatureCollection();
        //    await routes.ForEachAsync(r =>
        //    {
        //        var coordinates = r.Coordinates.Split('\n').ToList();
        //        var coords = coordinates.Select(r => new Position(double.Parse(r.Split(',')[1], CultureInfo.InvariantCulture), double.Parse(r.Split(',')[0], CultureInfo.InvariantCulture))).ToList();
        //        var geo = new GeoJSON.Net.Geometry.LineString(coords);
        //        var feature = new GeoJSON.Net.Feature.Feature(geo);
        //        feature.Properties.Add("name", r.Name);
        //        feature.Properties.Add("type", r.RouteType.Name);
        //        feature.Properties.Add("description", r.Description);
        //        feature.Properties.Add("lineNumber", r.LineNumber);
        //        feature.Properties.Add("operatingCompany", r.OperatingCompany);
        //        feature.Properties.Add("stroke", r.RouteType.Colour);
        //        collection.Features.Add(feature);
        //    });

        //    return collection;
        //}
    }
}
