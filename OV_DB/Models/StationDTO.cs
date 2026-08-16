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
        /// What the visit means. Every visit is at least a stop, so an undated row - including all
        /// those predating visit history - counts as <see cref="StationVisitLevel.Stopped"/> rather
        /// than as an unknown. Dating is a separate question from level.
        /// </summary>
        public StationVisitLevel? VisitLevel { get; set; }
        /// <summary>
        /// When the visit happened, where that is known. Null is the ordinary case for now — the web
        /// marks without claiming a date — and is what the backfill exists to fill in.
        /// </summary>
        public DateTime? FirstStoppedDate { get; set; }
        public DateTime? FirstEntryExitDate { get; set; }
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
