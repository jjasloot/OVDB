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
        public List<RegionStatDTO> Children { get; set; } = new();
    }
}
