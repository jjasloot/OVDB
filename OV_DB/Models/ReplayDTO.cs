using System;
using System.Collections.Generic;

namespace OV_DB.Models
{
    /// <summary>
    /// Routes ordered by the first time they were ridden, for a replay that draws them progressively
    /// along a timeline.
    /// </summary>
    /// <remarks>
    /// Geometry is simplified to ~25 m. Measured on this user's default map: 4,295 routes hold 3.5M
    /// coordinates and about 70 MB of JSON unsimplified, which is not a payload; 25 m brings it to
    /// 232k points and 4.6 MB while staying accurate well past any zoom a replay is watched at.
    /// </remarks>
    public class ReplayDTO
    {
        public DateTime? Start { get; set; }
        public DateTime? End { get; set; }
        public List<ReplayRouteDTO> Routes { get; set; } = [];

        /// <summary>
        /// When each dated visit first became a stop, sorted. Undated visits are absent: the replay
        /// shows what is known, not a guess at when the rest happened.
        /// </summary>
        public List<DateTime> StoppedDates { get; set; } = [];

        /// <summary>
        /// When each dated visit first became a got-on/off, sorted. A subset of
        /// <see cref="StoppedDates"/> by date or later, so the difference between the two counts is
        /// the stations still only stopped at — and a station moving between them is the upgrade.
        /// </summary>
        public List<DateTime> EntryExitDates { get; set; } = [];

        /// <summary>
        /// The same station growth broken down by country, so the replay shows where it happened and
        /// not just how much. Only countries with at least one dated visit are included — three dozen
        /// empty bars would say nothing.
        /// </summary>
        public List<ReplayRegionDTO> Regions { get; set; } = [];
    }

    /// <summary>One country's station progress over the replay's timeline.</summary>
    public class ReplayRegionDTO
    {
        public int RegionId { get; set; }
        public string Name { get; set; }
        public string NameNL { get; set; }
        public string FlagEmoji { get; set; }
        /// <summary>Active stations in the country, which is what the bar fills towards.</summary>
        public int TotalStations { get; set; }
        public List<DateTime> StoppedDates { get; set; } = [];
        public List<DateTime> EntryExitDates { get; set; } = [];
    }

    public class ReplayRouteDTO
    {
        public int RouteId { get; set; }
        public string Name { get; set; }
        public string NameNL { get; set; }
        /// <summary>The colour actually used: the route's override where it has one.</summary>
        public string Colour { get; set; }
        /// <summary>The type's own colour, so the replay can turn overrides off and compare.</summary>
        public string RouteTypeColour { get; set; }
        public DateTime FirstDate { get; set; }
        public double DistanceKm { get; set; }
        /// <summary>Latitude/longitude pairs, ready for Leaflet.</summary>
        public List<double[]> Coordinates { get; set; } = [];
    }

    /// <summary>
    /// Every station, with the two dates the station replay animates. Not scoped to a station map:
    /// see <c>StatsController.GetStationReplay</c> for why the coverage circles need the lot.
    /// </summary>
    public class StationReplayDTO
    {
        public List<StationReplayStationDTO> Stations { get; set; } = [];
    }

    public class StationReplayStationDTO
    {
        public int Id { get; set; }
        public string Name { get; set; }
        public double Lat { get; set; }
        public double Lon { get; set; }
        /// <summary>
        /// Carried separately from the dates because a visit is allowed to have neither: most of
        /// them predate visit history, and "visited, date unknown" is not the same as "not visited".
        /// </summary>
        public bool Visited { get; set; }
        public DateTime? Stopped { get; set; }
        public DateTime? EntryExit { get; set; }
    }
}
