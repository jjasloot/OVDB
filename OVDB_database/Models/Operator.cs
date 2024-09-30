using System.Collections.Generic;

namespace OVDB_database.Models;

public class Operator
{
    public int Id { get; set; }
    public List<string> Names { get; set; }
    public List<Region> RunsTrainsInRegions { get; set; }
    public List<Region> RestrictToRegions { get; set; }
    public string? LogoFilePath { get; set; }
    public string? LogoContentType { get; set; }
    public ICollection<Route> Routes { get; set; } = [];
}
