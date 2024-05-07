using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Text;

namespace OVDB_database.Models
{
    public class StationGrouping
    {
        [Key]
        [JsonProperty]
        public int Id { get; set; }
        public int UserId { get; set; }
        [JsonProperty]
        public string Name { get; set; }
        [JsonProperty]
        public string NameNL { get; set; }
        [JsonProperty]
        public Guid MapGuid { get; set; }
        [JsonProperty]
        public string? SharingLinkName { get; set; }
        public int OrderNr { get; set; }
        public User? User { get; set; }
        public List<Region> Regions { get; set; }
    }
}
