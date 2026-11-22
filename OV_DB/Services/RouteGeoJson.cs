using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;
using NetTopologySuite.Geometries;
using NetTopologySuite.IO.Converters;
using OVDB_database.Models;

namespace OV_DB.Services
{
    /// <summary>
    /// OVDB's export format: a GeoJSON FeatureCollection with one LineString feature per route and
    /// everything needed to recreate it (route fields, type, maps, operators, every trip) in its
    /// properties. Plain GeoJSON lines from other tools (e.g. BRouter) read as routes with only a name.
    /// </summary>
    public static class RouteGeoJson
    {
        public static readonly JsonSerializerOptions Options = new(JsonSerializerDefaults.Web)
        {
            Converters = { new GeoJsonConverterFactory() },
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        };

        public class FeatureCollection
        {
            public string Type { get; set; } = "FeatureCollection";
            public List<Feature> Features { get; set; } = [];
        }

        public class Feature
        {
            public string Type { get; set; } = "Feature";
            public Geometry Geometry { get; set; }
            public RouteProperties Properties { get; set; }
        }

        public class RouteProperties
        {
            /// <summary>The route's share id; used on import to recognise a route that is already there.</summary>
            public Guid? Share { get; set; }
            public string Name { get; set; }
            public string NameNL { get; set; }
            public string Description { get; set; }
            public string DescriptionNL { get; set; }
            public string From { get; set; }
            public string To { get; set; }
            public string LineNumber { get; set; }
            public string OperatingCompany { get; set; }
            public string OverrideColour { get; set; }
            public double? OverrideDistance { get; set; }
            public string TrainlogType { get; set; }
            public RouteTypeProperties RouteType { get; set; }
            public List<string> Maps { get; set; }
            public List<int> OperatorIds { get; set; }
            public List<TripProperties> Trips { get; set; }
        }

        public class RouteTypeProperties
        {
            public string Name { get; set; }
            public string NameNL { get; set; }
            public string Colour { get; set; }
            public bool IsTrain { get; set; }
            public string TrainlogType { get; set; }
        }

        public class TripProperties
        {
            public DateTime Date { get; set; }
            public DateTime? StartTime { get; set; }
            public DateTime? EndTime { get; set; }
            public DateTime? ScheduledStartTime { get; set; }
            public DateTime? ScheduledEndTime { get; set; }
            public double? DurationHours { get; set; }
            public int? TraewellingStatusId { get; set; }
            public string TraewellingStatusUuid { get; set; }
            public List<string> Maps { get; set; }
            public List<TagProperties> Tags { get; set; }
        }

        public class TagProperties
        {
            public string Key { get; set; }
            public string Value { get; set; }
            public bool? Bool { get; set; }
        }

        /// <summary>
        /// Needs RouteType, RouteMaps.Map, Operators and RouteInstances with their properties and
        /// RouteInstanceMaps.Map loaded.
        /// </summary>
        public static Feature ToFeature(Route route) => new()
        {
            Geometry = route.LineString,
            Properties = new RouteProperties
            {
                Share = route.Share,
                Name = route.Name,
                NameNL = route.NameNL,
                Description = route.Description,
                DescriptionNL = route.DescriptionNL,
                From = route.From,
                To = route.To,
                LineNumber = route.LineNumber,
                OperatingCompany = route.OperatingCompany,
                OverrideColour = route.OverrideColour,
                OverrideDistance = route.OverrideDistance,
                TrainlogType = route.TrainlogType,
                RouteType = route.RouteType == null ? null : new RouteTypeProperties
                {
                    Name = route.RouteType.Name,
                    NameNL = route.RouteType.NameNL,
                    Colour = route.RouteType.Colour,
                    IsTrain = route.RouteType.IsTrain,
                    TrainlogType = route.RouteType.TrainlogType,
                },
                Maps = route.RouteMaps.Select(rm => rm.Map.Name).ToList(),
                OperatorIds = route.Operators?.Select(o => o.Id).ToList(),
                Trips = route.RouteInstances.OrderBy(ri => ri.Date).Select(ri => new TripProperties
                {
                    Date = ri.Date,
                    StartTime = ri.StartTime,
                    EndTime = ri.EndTime,
                    ScheduledStartTime = ri.ScheduledStartTime,
                    ScheduledEndTime = ri.ScheduledEndTime,
                    DurationHours = ri.DurationHours,
                    TraewellingStatusId = ri.TrawellingStatusId,
                    TraewellingStatusUuid = ri.TrawellingStatusUuid,
                    Maps = ri.RouteInstanceMaps?.Select(m => m.Map.Name).ToList(),
                    Tags = ri.RouteInstanceProperties?.Select(p => new TagProperties { Key = p.Key, Value = p.Value, Bool = p.Bool }).ToList(),
                }).ToList(),
            }
        };

        /// <summary>
        /// The route line, 2D. Other tools write elevation as a third ordinate, and a single-part
        /// MultiLineString is the same line. Null when the geometry is not one line.
        /// </summary>
        public static LineString ToLine(Geometry geometry)
        {
            var line = geometry switch
            {
                LineString l => l,
                MultiLineString { NumGeometries: 1 } m => (LineString)m.GetGeometryN(0),
                _ => null
            };
            return line == null || line.NumPoints < 2 ? null : new LineString(line.Coordinates.Select(c => new Coordinate(c.X, c.Y)).ToArray());
        }

        /// <summary>
        /// What makes two trips on one route the same trip, so a re-import adds only the missing ones.
        /// </summary>
        public static string TripKey(int? traewellingStatusId, DateTime date, DateTime? start, DateTime? end) =>
            traewellingStatusId?.ToString() ?? $"{date:O}|{start:O}|{end:O}";
    }
}
