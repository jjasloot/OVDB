using Newtonsoft.Json;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace OVDB_database.Models
{
    [JsonObject(MemberSerialization.OptIn)]
    public class Country
    {
        [Key]
        [JsonProperty]
        public int CountryId { get; set; }
        [Required]
        [JsonProperty]
        public string Name { get; set; }
        [JsonProperty]
        public string NameNL { get; set; }
        public int OrderNr { get; set; }
        public int UserId { get; set; }
        public List<RouteCountry> RouteCountries { get; set; }
    }
}
