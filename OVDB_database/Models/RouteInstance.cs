using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Text;

namespace OVDB_database.Models
{
    public class RouteInstance
    {
        [Key]
        public int RouteInstanceId { get; set; }
        public int RouteId { get; set; }
        public Route Route { get; set; }
        public DateTime Date { get; set; }
        public List<RouteInstanceProperty> RouteInstanceProperties { get; set; }
        public List<RouteInstanceMap> RouteInstanceMaps { get; set; }
    }
}
