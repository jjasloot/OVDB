using System.ComponentModel.DataAnnotations;

namespace OVDB_database.Models
{
    public class RouteCountry
    {
        [Key]
        public long RouteCountryId { get; set; }
        public int RouteId { get; set; }
        public int CountryId { get; set; }
        public Route Route { get; set; }
        public Country Country { get; set; }
    }
}
