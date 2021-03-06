﻿using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Text;

namespace OVDB_database.Models
{
    public class StationCountry
    {
        [Key]
        [JsonProperty]
        public int Id { get; set; }
        public string OsmId { get; set; }
        [JsonProperty]
        public string Name { get; set; }
        [JsonProperty]
        public string NameNL { get; set; }
        public List<StationMapCountry> StationMapCountries { get; set; }
        public List<Station> Stations { get; set; }

    }
}
