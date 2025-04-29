namespace OV_DB.Models;

public class RegionOperatorsDTO
{
    public int RegionId { get; set; }
    public string RegionName { get; set; }
    public List<RegionOperatorDTO> Operators { get; set; }
}

public class RegionOperatorDTO
{
    public int OperatorId { get; set; }
    public List<string> OperatorNames { get; set; }
    public string? LogoFilePath { get; set; }
    public bool HasUserRoute { get; set; }
}
