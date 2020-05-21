using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;

namespace OV_DB.Models
{
    public partial class OSM
    {
        [JsonProperty("version")]
        public double Version { get; set; }

        [JsonProperty("generator")]
        public string Generator { get; set; }

        [JsonProperty("osm3s")]
        public Osm3S Osm3S { get; set; }

        [JsonProperty("elements")]
        public List<Element> Elements { get; set; }
    }

    public partial class Element
    {
        [JsonProperty("type")]
        public TypeEnum Type { get; set; }

        [JsonProperty("id")]
        public long Id { get; set; }

        [JsonProperty("lat", NullValueHandling = NullValueHandling.Ignore)]
        public double? Lat { get; set; }

        [JsonProperty("lon", NullValueHandling = NullValueHandling.Ignore)]
        public double? Lon { get; set; }

        [JsonProperty("tags", NullValueHandling = NullValueHandling.Ignore)]
        public Dictionary<string, string> Tags { get; set; }

        [JsonProperty("nodes", NullValueHandling = NullValueHandling.Ignore)]
        public List<long> Nodes { get; set; }

        [JsonProperty("members", NullValueHandling = NullValueHandling.Ignore)]
        public List<Member> Members { get; set; }
    }

    public partial class Member
    {
        [JsonProperty("type")]
        public TypeEnum Type { get; set; }

        [JsonProperty("ref")]
        public long Ref { get; set; }

        [JsonProperty("role")]
        public string Role { get; set; }
    }

    public partial class Osm3S
    {
        [JsonProperty("timestamp_osm_base")]
        public DateTimeOffset TimestampOsmBase { get; set; }

        [JsonProperty("copyright")]
        public string Copyright { get; set; }
    }

    public enum Role { Empty, Platform };

    public enum TypeEnum { Node, Relation, Way };

    internal static class Converter
    {
        public static readonly JsonSerializerSettings Settings = new JsonSerializerSettings
        {
            MetadataPropertyHandling = MetadataPropertyHandling.Ignore,
            DateParseHandling = DateParseHandling.None,
            Converters =
            {
                RoleConverter.Singleton,
                TypeEnumConverter.Singleton,
                new IsoDateTimeConverter { DateTimeStyles = DateTimeStyles.AssumeUniversal }
            },
        };
    }

    internal class RoleConverter : JsonConverter
    {
        public override bool CanConvert(Type t) => t == typeof(Role) || t == typeof(Role?);

        public override object ReadJson(JsonReader reader, Type t, object existingValue, JsonSerializer serializer)
        {
            if (reader.TokenType == JsonToken.Null) return null;
            var value = serializer.Deserialize<string>(reader);
            switch (value)
            {
                case "":
                    return Role.Empty;
                case "platform":
                    return Role.Platform;
                default:
                    return Role.Empty;
            }
            throw new Exception("Cannot unmarshal type Role");
        }

        public override void WriteJson(JsonWriter writer, object untypedValue, JsonSerializer serializer)
        {
            if (untypedValue == null)
            {
                serializer.Serialize(writer, null);
                return;
            }
            var value = (Role)untypedValue;
            switch (value)
            {
                case Role.Empty:
                    serializer.Serialize(writer, "");
                    return;
                case Role.Platform:
                    serializer.Serialize(writer, "platform");
                    return;
            }
            throw new Exception("Cannot marshal type Role");
        }

        public static readonly RoleConverter Singleton = new RoleConverter();
    }

    internal class TypeEnumConverter : JsonConverter
    {
        public override bool CanConvert(Type t) => t == typeof(TypeEnum) || t == typeof(TypeEnum?);

        public override object ReadJson(JsonReader reader, Type t, object existingValue, JsonSerializer serializer)
        {
            if (reader.TokenType == JsonToken.Null) return null;
            var value = serializer.Deserialize<string>(reader);
            switch (value)
            {
                case "node":
                    return TypeEnum.Node;
                case "relation":
                    return TypeEnum.Relation;
                case "way":
                    return TypeEnum.Way;
            }
            throw new Exception("Cannot unmarshal type TypeEnum");
        }

        public override void WriteJson(JsonWriter writer, object untypedValue, JsonSerializer serializer)
        {
            if (untypedValue == null)
            {
                serializer.Serialize(writer, null);
                return;
            }
            var value = (TypeEnum)untypedValue;
            switch (value)
            {
                case TypeEnum.Node:
                    serializer.Serialize(writer, "node");
                    return;
                case TypeEnum.Relation:
                    serializer.Serialize(writer, "relation");
                    return;
                case TypeEnum.Way:
                    serializer.Serialize(writer, "way");
                    return;
            }
            throw new Exception("Cannot marshal type TypeEnum");
        }

        public static readonly TypeEnumConverter Singleton = new TypeEnumConverter();
    }
}
