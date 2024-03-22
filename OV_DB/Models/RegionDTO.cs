using DocumentFormat.OpenXml.Office2010.ExcelAc;
using System.Collections.Generic;

namespace OV_DB.Models;

public class RegionDTO
{
    public int Id { get; set; }
    public string Name { get; set; } = null!;
    public string NameNL { get; set; } = null!;
    public string OriginalName { get; set; } = null!;
    public long OsmRelationId { get; set; }
    public IEnumerable<RegionDTO> SubRegions { get; set; } = [];
}

public class RegionMinimalDTO
{
    public int Id { get; set; }
    public string Name { get; set; } = null!;
    public string NameNL { get; set; } = null!;
    public string OriginalName { get; set; } = null!;
}

public class RegionIntermediate
{
    public int Id { get; set; }
    public string Name { get; set; } = null!;
    public string NameNL { get; set; } = null!;
    public string OriginalName { get; set; } = null!;
    public long OsmRelationId { get; set; }
    public int? ParentRegionId { get; set; }
}
