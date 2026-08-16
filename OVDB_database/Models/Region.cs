using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using Microsoft.EntityFrameworkCore;

namespace OVDB_database.Models;

[Index(nameof(OsmRelationId))]
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
    public string? IsoCode { get; set; }

    /// <summary>
    /// How many levels below this country the "collect the regions" achievement counts. Only
    /// meaningful on top-level countries. One - the level directly underneath - suits almost
    /// everything (provinces, Bundesländer, cantons); raise it where the first level is too
    /// coarse to be interesting, such as the United Kingdom's four nations.
    /// </summary>
    public int AchievementRegionDepth { get; set; } = 1;
}
