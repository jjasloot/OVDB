using System;
using System.Collections.Generic;

namespace OV_DB.Models
{
    /// <summary>
    /// Routes ordered by the first time they were ridden, with geometry simplified for animation
    /// rather than for accuracy - the replay draws them progressively over a timeline.
    /// </summary>
    public class ReplayDTO
    {
        public DateTime? Start { get; set; }
        public DateTime? End { get; set; }
        public List<ReplayRouteDTO> Routes { get; set; } = [];
    }

    public class ReplayRouteDTO
    {
        public int RouteId { get; set; }
        public string Name { get; set; }
        public string NameNL { get; set; }
        public string Colour { get; set; }
        public DateTime FirstDate { get; set; }
        public double DistanceKm { get; set; }
        /// <summary>Latitude/longitude pairs, ready for Leaflet.</summary>
        public List<double[]> Coordinates { get; set; } = [];
    }
}
