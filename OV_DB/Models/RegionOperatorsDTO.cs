using System.Collections.Generic;

namespace OV_DB.Models;

public class RegionOperatorsDTO
{
    public int RegionId { get; set; }
    public string Name { get; set; }
    public List<RegionOperatorDTO> Operators { get; set; }
    public string OriginalName { get; set; }
    public string NameNL { get; set; }
}

public class RegionOperatorDTO
{
    public int OperatorId { get; set; }
    public List<string> OperatorNames { get; set; }
    public bool HasUserRoute { get; set; }
}
