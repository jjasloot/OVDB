using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Text;

namespace OVDB_database.Models
{
    public class RouteInstanceMap
    {
        [Key]
        public long RouteMapId { get; set; }
        public int RouteInstanceId { get; set; }
        public int MapId { get; set; }
        public RouteInstance RouteInstance { get; set; }
        public Map Map { get; set; }
    }
}
