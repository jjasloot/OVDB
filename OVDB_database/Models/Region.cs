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
    public IEnumerable<Region> SubRegions { get; set; } = null!;
    public IEnumerable<Route> Routes { get; set; } = null!;
}
