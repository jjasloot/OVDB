using Newtonsoft.Json;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace OVDB_database.Models
{
    [JsonObject(MemberSerialization.OptIn)]

    public class RouteType
    {
        [Key]
        [JsonProperty]
        public int TypeId { get; set; }
        [Required]
        [JsonProperty]
        public string Name { get; set; }
        [JsonProperty]
        public string NameNL { get; set; }
        [Required]
        [JsonProperty]
        public string Colour { get; set; }
        public int UserId { get; set; }
        public int OrderNr { get; set; }
        public List<Route> Routes { get; set; }

    }
}
