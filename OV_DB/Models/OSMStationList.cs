using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace OV_DB.Models
{
    public class OSMStationElement
    {
        [JsonProperty("type")]
        public string Type { get; set; }

        [JsonProperty("id")]
        public long Id { get; set; }

        [JsonProperty("lat")]
        public double Lat { get; set; }

        [JsonProperty("lon")]
        public double Lon { get; set; }

        [JsonProperty("tags")]
        public Dictionary<string,string> Tags { get; set; }
    }

    public class OSMStationList
    {
        [JsonProperty("elements")]
        public List<OSMStationElement> Elements { get; set; }
    }


}
