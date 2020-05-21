using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Text;

namespace OVDB_database.Models
{
    public class RouteMap
    {
        [Key]
        public long RouteMapId { get; set; }
        public int RouteId { get; set; }
        public int MapId { get; set; }
        public Route Route { get; set; }
        public Map Map { get; set; }
    }
}
