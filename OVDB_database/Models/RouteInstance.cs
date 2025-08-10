using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Text;

namespace OVDB_database.Models
{
    [Index(nameof(Date))]
    public class RouteInstance
    {
        [Key]
        public int RouteInstanceId { get; set; }
        public int RouteId { get; set; }
        public Route Route { get; set; }
        public DateTime Date { get; set; }
        public DateTime? StartTime { get; set; }
        public DateTime? EndTime { get; set; }
        public List<RouteInstanceProperty> RouteInstanceProperties { get; set; }
        public List<RouteInstanceMap> RouteInstanceMaps { get; set; }
    }
}
