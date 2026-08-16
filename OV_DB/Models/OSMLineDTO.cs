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
        /// <summary>
        /// The stations this relation calls at along the section being imported. Parsed from the
        /// relation's <c>stop</c> and <c>platform</c> members, which is the operator's own calling
        /// pattern — the one thing route geometry cannot tell you, because a line passing a station
        /// looks identical whether or not the train stops.
        /// </summary>
        public List<OSMStopDTO> Stops { get; set; } = [];
    }

    public class OSMStopDTO
    {
        public string Name { get; set; }
        public double Lattitude { get; set; }
        public double Longitude { get; set; }
    }
}
