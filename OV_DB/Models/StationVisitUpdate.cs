using System;

namespace OV_DB.Models
{
    /// <summary>
    /// What a visit means. Not stored: the level is derived from which dates are set on the row, so
    /// this exists only to say what the caller is asserting.
    /// </summary>
    public enum StationVisitLevel
    {
        /// <summary>A train the user was aboard stopped here. The weaker, default claim.</summary>
        Stopped = 0,
        /// <summary>The user got on or off here. Implies <see cref="Stopped"/>.</summary>
        EntryExit = 1
    }

    public class StationVisitUpdate
    {
        public bool Visited { get; set; }
        public StationVisitLevel Level { get; set; }
        /// <summary>
        /// Local date of the visit. Null leaves the visit undated, which is valid: it simply joins
        /// the backfill queue. The web map sends null, because marking from the sofa says nothing
        /// about when.
        /// </summary>
        public DateTime? Date { get; set; }
    }

    public class StationVisitStateDTO
    {
        public bool Visited { get; set; }
        public StationVisitLevel? Level { get; set; }
        public DateTime? FirstStoppedDate { get; set; }
        public DateTime? FirstEntryExitDate { get; set; }
        public double PercentageVisited { get; set; }
    }
}
