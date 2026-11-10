using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Text;

namespace OVDB_database.Models
{
    // A route is on a map once; this also serves the RouteId foreign key.
    [Microsoft.EntityFrameworkCore.Index(nameof(RouteId), nameof(MapId), IsUnique = true)]
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
