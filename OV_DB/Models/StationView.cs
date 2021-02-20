using GeoJSON.Net.Feature;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace OV_DB.Models
{
    public class StationView
    {
        public List<StationDTO> Stations { get; set; }
        public int Total { get; set; }
        public int Visited { get; set; }
        public string Name { get; set; }
        public string NameNL { get; set; }
    }
}
