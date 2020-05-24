using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;
using GeoJSON.Net.Feature;
using GeoJSON.Net.Geometry;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using OVDB_database.Models;

namespace OV_DB.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class GeoJSONController : ControllerBase
    {
        public void Test()
        {
            var route = new Route();
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
            if (!string.IsNullOrWhiteSpace(route.Name))
                feature.Properties.Add("name", route.Name);
            if (!string.IsNullOrWhiteSpace(route.NameNL))
                feature.Properties.Add("nameNL", route.NameNL);
            if (!string.IsNullOrWhiteSpace(route.Description))
                feature.Properties.Add("description", route.Description);
            if (!string.IsNullOrWhiteSpace(route.DescriptionNL))
                feature.Properties.Add("descriptionNL", route.DescriptionNL);
            if (route.FirstDateTime.HasValue)
                feature.Properties.Add("firstDateTime", route.FirstDateTime.Value.ToString("o"));
            if (!string.IsNullOrWhiteSpace(route.LineNumber))
                feature.Properties.Add("lineNumber", route.LineNumber);
            if (!string.IsNullOrWhiteSpace(route.OperatingCompany))
                feature.Properties.Add("operatingCompany", route.OperatingCompany);
            if (!string.IsNullOrWhiteSpace(route.OverrideColour))
                feature.Properties.Add("name", route.OverrideColour);
            if (route.RouteCountries.Any())
                feature.Properties.Add("countries", string.Join(',', route.RouteCountries.Select(c => c.Country.Name)));
            if (route.RouteMaps.Any())
                feature.Properties.Add("maps", string.Join(',', route.RouteMaps.Select(c => c.Map.Name)));
            if (route.RouteTypeId.HasValue)
                feature.Properties.Add("type", route.RouteType.Name);
            collection.Features.Add(feature);
        }
    }
}