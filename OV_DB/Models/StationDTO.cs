using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace OV_DB.Models
{
    public class StationDTO
    {
        public int Id { get; set; }
        public string Name { get; set; }
        public double Lattitude { get; set; }
        public double Longitude { get; set; }
        public double? Elevation { get; set; }
        public String Network { get; set; }
        public String Operator { get; set; }
        public bool Visited { get; set; }
        /// <summary>
        /// What the visit means, when known. Null while <see cref="Visited"/> is true is a real
        /// state, not a gap: rows that predate visit history are visited with no level yet, and the
        /// map shows them distinctly until the backfill has been through them.
        /// </summary>
        public StationVisitLevel? VisitLevel { get; set; }
        public IEnumerable<StationRegionDTO> Regions { get; set; }
    }

    public class StationRegionDTO
    {
        public int Id { get; set; }
        public string OriginalName { get; set; }
        public bool HasParentRegion { get; set; }
        public string FlagEmoji { get; set; }
    }
}
