using GeoJSON.Net.Feature;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace OV_DB.Models
{
    public class OSMLineDTO
    {
        public long Id { get; set; }
        public string Description { get; set; }
        public string Network { get; set; }
        public string Operator { get; set; }
        public string From { get; set; }
        public string To { get; set; }
        public string Name { get; set; }
        public string PotentialErrors { get; set; }
        public FeatureCollection GeoJson { get; set; }
        public string Ref { get; set; }
        public string Colour { get; set; }
    }
}
