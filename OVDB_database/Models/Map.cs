using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Text;
using Microsoft.EntityFrameworkCore;

namespace OVDB_database.Models
{
    [JsonObject(MemberSerialization.OptIn)]
    [Index(nameof(MapGuid), IsUnique = true)]
    public class Map
    {
        [Key]
        [JsonProperty]
        public int MapId { get; set; }
        public int UserId { get; set; }
        [JsonProperty]
        public string Name { get; set; }
        [JsonProperty]
        public string NameNL { get; set; }
        [JsonProperty]
        public Guid MapGuid { get; set; }
        [JsonProperty]
        public string? SharingLinkName { get; set; }
        [JsonProperty]
        public bool Default { get; set; } = false;
        [JsonProperty]
        public bool ShowRouteInfo { get; set; } = true;
        [JsonProperty]
        public bool ShowRouteOutline { get; set; } = true;
        public int OrderNr { get; set; }

        public User? User { get; set; }
        public List<RouteMap> RouteMaps { get; set; }
    }
}
