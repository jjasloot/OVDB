using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Text;

namespace OVDB_database.Models
{
    public class Station
    {
        [Key]
        public int Id { get; set; }
        public long OsmId { get; set; }
        public string Name { get; set; }
        public double Lattitude { get; set; }
        public double Longitude { get; set; }
        public double? Elevation { get; set; }
        public String Network { get; set; }
        public String Operator { get; set; }
        public int? StationCountryId { get; set; }
        public StationCountry StationCountry { get; set; }
        public List<StationVisit> StationVisits { get; set; }
        public bool Hidden { get; set; }
        public bool Special { get; set; }
        public ICollection<Region> Regions { get; set; } = new List<Region>();
    }
}
