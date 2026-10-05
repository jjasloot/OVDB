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
        [JsonProperty]
        public bool IsTrain { get; set; }
        [JsonProperty]
        public string TrainlogType { get; set; }
        /// <summary>
        /// Routes of this type run on rails and can cover network edges (trains, trams, metros).
        /// OVDB-native: network matching must not depend on TrainlogType, which is Trainlog's
        /// export vocabulary, nor on IsTrain, which means "train" specifically for statistics.
        /// </summary>
        [JsonProperty]
        public bool MatchesRailNetworks { get; set; }
    }
}
