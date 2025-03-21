using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Text;
using System.Linq;

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

        public bool Visited { get; set; }

        public static List<Station> GetNearbyStations(OVDBDatabaseContext dbContext, double latitude, double longitude, double radius)
        {
            return dbContext.Stations
                .Where(s => (s.Lattitude - latitude) * (s.Lattitude - latitude) + (s.Longitude - longitude) * (s.Longitude - longitude) <= radius * radius)
                .OrderBy(s => (s.Lattitude - latitude) * (s.Lattitude - latitude) + (s.Longitude - longitude) * (s.Longitude - longitude))
                .Take(5)
                .ToList();
        }
    }
}
