namespace OVDB_database.Enums
{
    /// <summary>
    /// How a station visit came to exist. Every value denotes an explicit user action: nothing may
    /// create a <see cref="Models.StationVisit"/> without one.
    /// </summary>
    public enum StationVisitSource
    {
        /// <summary>Predates visit history; no date and no known level.</summary>
        Legacy = 0,
        Web = 1,
        Telegram = 2,
        /// <summary>Ticked from the suggestions shown after importing a route.</summary>
        ImportSuggested = 3,
        /// <summary>Dated during the backfill review.</summary>
        Backfill = 4
    }
}
