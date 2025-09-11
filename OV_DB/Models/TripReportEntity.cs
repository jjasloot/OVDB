using OVDB_database.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace OV_DB.Models
{
    public class TripReportEntity
    {
        public DateTime Date { get; set; }
        public string Type { get; set; }
        public string TypeNL { get; set; }
        public string Line { get; set; }
        public string Operator { get; set; }
        public string Description { get; set; }
        public string DescriptionNL { get; set; }
        public string Name { get; set; }
        public string NameNL { get; set; }
        public string From { get; set; }
        public string To { get; set; }
        public DateTime? StartTime { get; set; }
        public DateTime? EndTime { get; set; }
        public double Distance { get; set; }
        public List<RouteInstanceProperty> ExtraInfo { get; set; } = new List<RouteInstanceProperty>();
        public double? Duration { get; set; }
    }
}
