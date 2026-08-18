using System.Collections.Generic;

namespace OV_DB.Models
{
    public class RegionStatDTO
    {
        public int Id { get; set; }
        public string Name { get; set; }
        public string NameNL { get; set; }
        public string OriginalName { get; set; }
        public long OsmRelationId { get; set; }
        public bool Visited { get; set; }
        public int TotalStations { get; set; }
        public int VisitedStations { get; set; }
        /// <summary>
        /// Of the visited ones, how many you actually got on or off at. A subset of
        /// <see cref="VisitedStations"/>, never a separate total.
        /// </summary>
        public int EntryExitStations { get; set; }
        public List<RegionStatDTO> Children { get; set; } = new();
        public string? FlagEmoji { get; set; }
        public int? ParentRegionId { get; set; }
    }
}
