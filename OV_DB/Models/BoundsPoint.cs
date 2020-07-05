using OVDB_database.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace OV_DB.Models
{
    public class BoundsPoint
    {
        public double Lat { get; set; }
        public double Long { get; set; }
        public Route Route { get; set; }
    }
}
