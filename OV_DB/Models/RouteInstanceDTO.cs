using OVDB_database.Models;
using System;
using System.Collections.Generic;

namespace OV_DB.Models
{
    public class RouteInstanceDTO
    {
        public int RouteInstanceId { get; set; }
        public int RouteId { get; set; }
        public Guid Share { get; set; }
        public DateTime Date { get; set; }
        public DateTime? StartTime { get; set; }
        public DateTime? EndTime { get; set; }
        public double? DurationHours { get; set; }
        public double? AverageSpeedKmh { get; set; }
        public List<RouteInstancePropertyDTO> RouteInstanceProperties { get; set; }
        public List<RouteInstanceMapDTO> RouteInstanceMaps { get; set; }
    }

    public class RouteInstancePropertyDTO
    {
        public long? RouteInstancePropertyId { get; set; }
        public string Key { get; set; }
        public string Value { get; set; }
        public bool? Bool { get; set; }
    }

    public class RouteInstanceMapDTO
    {
        public int MapId { get; set; }
        public string Name { get; set; }
        public string NameNL { get; set; }
    }
}