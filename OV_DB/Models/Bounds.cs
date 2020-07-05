using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace OV_DB.Models
{
    public class Bounds
    {
        public BoundsPoint LatMin { get; set; }
        public BoundsPoint LatMax { get; set; }
        public BoundsPoint LongMin { get; set; }
        public BoundsPoint LongMax { get; set; }
    }
}
