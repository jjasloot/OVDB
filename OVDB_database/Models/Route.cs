﻿using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace OVDB_database.Models
{
    [JsonObject(MemberSerialization.OptIn)]
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
        public string OverrideColour { get; set; }
        [JsonProperty]
        public string LineNumber { get; set; }
        [JsonProperty]
        public string OperatingCompany { get; set; }
        [JsonProperty]
        public DateTime? FirstDateTime { get; set; }
        [JsonProperty]
        public List<RouteCountry> RouteCountries { get; set; }
        [JsonProperty]
        public int? RouteTypeId { get; set; }
        [JsonProperty]
        public RouteType? RouteType { get; set; }
        public string Coordinates { get; set; }
        [JsonProperty]
        public List<RouteMap> RouteMaps { get; set; }
    }
}