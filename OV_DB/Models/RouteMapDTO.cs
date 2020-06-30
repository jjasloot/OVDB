using OVDB_database.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace OV_DB.Models
{
    public class RouteMapDTO
    {
        public long RouteMapId { get; set; }
        public int MapId { get; set; }
        public string Name { get; set; }
        public string NameNL { get; set; }
    }
}
