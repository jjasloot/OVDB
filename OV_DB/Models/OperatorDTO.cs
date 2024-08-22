using System.Collections.Generic;

namespace OV_DB.Models;

public class OperatorDTO
{
    public int Id { get; set; }
    public List<string> Names { get; set; }
    public List<RegionMinimalDTO> Regions { get; set; }
    public string? LogoFilePath { get; set; }
}

public class OperatorMinimalDTO
{
    public int Id { get; set; }
    public string Name { get; set; }
}

public class OperatorUpdateDTO
{
    public List<string> Names { get; set; }
    public List<int> RegionIds { get; set; }
}
