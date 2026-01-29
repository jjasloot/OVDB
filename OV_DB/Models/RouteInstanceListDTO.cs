using System.Collections.Generic;

namespace OV_DB.Models
{
    public class RouteInstanceListDTO : RouteInstanceDTO
    {
        public string RouteName { get; set; }
        public string RouteDescription { get; set; }
        public RouteTypeDTO RouteType { get; set; }
        public string RouteTypeColour { get; set; }
        public string From { get; set; }
        public string To { get; set; }
        public double Distance { get; set; }
        public string RouteOverrideColour { get; set; }
    }

    public class RouteInstanceListResponseDTO
    {
        public int Count { get; set; }
        public List<RouteInstanceListDTO> Instances { get; set; }
    }

    public class RouteTypeDTO
    {
        public string Name { get; set; }
        public string NameNL { get; set; }
    }
}
