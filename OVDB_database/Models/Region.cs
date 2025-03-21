using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace OVDB_database.Models;
public class Region
{
    [Key]
    public int Id { get; set; }
    public string OriginalName { get; set; } = "";
    public string Name { get; set; } = null!;
    public string NameNL { get; set; } = null!;
    public long OsmRelationId { get; set; }
    public NetTopologySuite.Geometries.MultiPolygon Geometry { get; set; } = null!;
    public int? ParentRegionId { get; set; }
    public Region? ParentRegion { get; set; }
    public ICollection<Region> SubRegions { get; set; } = null!;
    public ICollection<Route> Routes { get; set; } = null!;
    public ICollection<Station> Stations { get; set; } = new List<Station>();
    public ICollection<StationGrouping> StationGroupings { get; set; } = new List<StationGrouping>();
    public ICollection<Operator> OperatorsRunningTrains { get; set; } = [];
    public ICollection<Operator> OperatorsRestrictedToRegion { get; set; } = [];
    public string? FlagEmoji { get; set; }
}
