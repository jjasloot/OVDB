using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace OVDB_database.Models
{
    [JsonObject(MemberSerialization.OptIn)]
    public class User
    {
        [Key]
        public int Id { get; set; }
        public string Email { get; set; }
        public string Password { get; set; }
        public DateTime LastLogin { get; set; }
        public string RefreshToken { get; set; }
        public Guid Guid { get; set; }
        public List<Map> Maps { get; set; }
        public List<RouteType> RouteTypes { get; set; }
        public List<Country> Countries { get; set; }
    }
}
