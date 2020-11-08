using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace OV_DB.Models
{
    public class OSMStationWayElement
    {
        [JsonProperty("type")]
        public string Type { get; set; }

        [JsonProperty("id")]
        public long Id { get; set; }

        [JsonProperty("center")]
        public OSMStationWayCenter Center { get; set; }

        [JsonProperty("tags")]
        public Dictionary<string,string> Tags { get; set; }
    }

    public class OSMStationWayCenter
    {
        [JsonProperty("lat")]
        public double Lat { get; set; }

        [JsonProperty("lon")]
        public double Lon { get; set; }
    }

    public class OSMStationWayList
    {
        [JsonProperty("elements")]
        public List<OSMStationWayElement> Elements { get; set; }
    }


}
