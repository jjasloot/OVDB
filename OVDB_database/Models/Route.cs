using NetTopologySuite.Geometries;
using Microsoft.EntityFrameworkCore;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace OVDB_database.Models
{
    [JsonObject(MemberSerialization.OptIn)]
    [Index(nameof(Name))]
    [Index(nameof(Share))]
    public class Route
    {
        [Key]
        [JsonProperty]
        public int RouteId { get; set; }
        [Required]
        [JsonProperty]
        public string Name { get; set; }
        [JsonProperty]
        public string Description { get; set; }
        [JsonProperty]
        public string NameNL { get; set; }
        [JsonProperty]
        public string DescriptionNL { get; set; }
        [JsonProperty]
        public string From { get; set; }
        [JsonProperty]
        public string To { get; set; }
        [JsonProperty]
        public string OverrideColour { get; set; }
        [JsonProperty]
        public string LineNumber { get; set; }
        [JsonProperty]
        public string OperatingCompany { get; set; }
        [JsonProperty]
        public DateTime? FirstDateTime { get; set; }
        [JsonProperty]
        public int? RouteTypeId { get; set; }
        [JsonProperty]
        public RouteType? RouteType { get; set; }
        [JsonProperty]
        public double CalculatedDistance { get; set; }
        [JsonProperty]
        public double? OverrideDistance { get; set; }
        public LineString LineString { get; set; }
        [JsonProperty]
        public List<RouteMap> RouteMaps { get; set; }
        [JsonProperty]
        public Guid Share { get; set; }
        [JsonProperty]
        public List<RouteInstance> RouteInstances { get; set; }
        public List<Region> Regions { get; set; } = new();
        public List<Operator> Operators { get; set; }
    }
}
