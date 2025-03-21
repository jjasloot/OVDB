using OVDB_database.Models;
using System;
using System.Collections.Generic;

namespace OV_DB.Models
{
    public class RouteDTO
    {
        public int RouteId { get; set; }
        public string Name { get; set; }
        public string Description { get; set; }
        public string NameNL { get; set; }
        public string DescriptionNL { get; set; }
        public string From { get; set; }
        public string To { get; set; }
        public string OverrideColour { get; set; }
        public string LineNumber { get; set; }
        public string OperatingCompany { get; set; }
        public DateTime? FirstDateTime { get; set; }
        public int? RouteTypeId { get; set; }
        public RouteType? RouteType { get; set; }
        public double CalculatedDistance { get; set; }
        public double? OverrideDistance { get; set; }
        public List<RouteMapDTO> RouteMaps { get; set; }
        public int RouteInstancesCount { get; set; }
        public Guid Share { get; set; }
        public List<RegionMinimalDTO> Regions { get; set; }
        public List<int> OperatorIds { get; set; }
    }
}
