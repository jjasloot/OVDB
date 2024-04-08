using System.Collections.Generic;

namespace OV_DB.Models;

public class RouteListDTO
{
    public long Count { get; set; }
    public List<RouteDTO> Routes { get; set; }
}
