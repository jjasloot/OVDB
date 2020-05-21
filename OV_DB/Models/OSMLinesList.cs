using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace OV_DB.Models
{
    public partial class OsmLinesList
    {
        public List<LinesElement> Elements { get; set; }
    }

    public partial class LinesElement
    {
        public long Id { get; set; }
        public Dictionary<string, string> Tags { get; set; }
        public Center Center { get; set; }
    }

    public partial class Center
    {
        public double Lat { get; set; }
        public double Lon { get; set; }
    }

}