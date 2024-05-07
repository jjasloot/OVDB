using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace OV_DB.Models
{
    public class StationMapDTO
    {
        public int Id { get; set; }
        public string Name { get; set; }
        public string NameNL { get; set; }
        public string MapGuid { get; set; }
        public string? SharingLinkName { get; set; }
        public List<int> RegionIds { get; set; } = [];
    }
}
