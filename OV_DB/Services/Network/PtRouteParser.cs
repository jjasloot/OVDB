using System;
using System.Collections.Generic;
using System.IO;
using Newtonsoft.Json;

namespace OV_DB.Services.Network;

[Flags]
public enum PtService { None = 0, Train = 1, Urban = 2 }

/// <summary>
/// Streams public-transport route relations and answers, per way id, which kind of passenger
/// service runs over it. Route relations are the service-based truth that the usage-tag
/// heuristic only approximates: an untagged freight line has no passenger relation, and a
/// railway=rail track carrying only Stadtbahn relations is urban rail in practice.
/// </summary>
public static class PtRouteParser
{
    public static Dictionary<long, PtService> Parse(Stream stream)
    {
        var result = new Dictionary<long, PtService>();
        using var text = new StreamReader(stream);
        using var reader = new JsonTextReader(text);

        while (reader.Read())
        {
            if (reader.TokenType == JsonToken.PropertyName && (string)reader.Value == "elements")
            {
                reader.Read(); // StartArray
                while (reader.Read() && reader.TokenType == JsonToken.StartObject)
                    ParseRelation(reader, result);
                break;
            }
        }
        return result;
    }

    private static void ParseRelation(JsonTextReader reader, Dictionary<long, PtService> result)
    {
        string route = null;
        var wayIds = new List<long>(128);

        while (reader.Read() && reader.TokenType != JsonToken.EndObject)
        {
            if (reader.TokenType != JsonToken.PropertyName)
                continue;
            switch ((string)reader.Value)
            {
                case "members":
                    reader.Read(); // StartArray
                    while (reader.Read() && reader.TokenType == JsonToken.StartObject)
                    {
                        string memberType = null, role = null;
                        long refId = 0;
                        while (reader.Read() && reader.TokenType != JsonToken.EndObject)
                        {
                            if (reader.TokenType != JsonToken.PropertyName)
                                continue;
                            switch ((string)reader.Value)
                            {
                                case "type": memberType = reader.ReadAsString(); break;
                                case "ref": reader.Read(); refId = Convert.ToInt64(reader.Value); break;
                                case "role": role = reader.ReadAsString(); break;
                                default:
                                    reader.Read();
                                    if (reader.TokenType is JsonToken.StartObject or JsonToken.StartArray)
                                        reader.Skip();
                                    break;
                            }
                        }
                        // Track members only; platform/stop members reference other things.
                        if (memberType == "way" && (string.IsNullOrEmpty(role) || role is "forward" or "backward" or "alternate" or "main"))
                            wayIds.Add(refId);
                    }
                    break;
                case "tags":
                    reader.Read(); // StartObject
                    while (reader.Read() && reader.TokenType == JsonToken.PropertyName)
                    {
                        var key = (string)reader.Value;
                        var value = reader.ReadAsString();
                        if (key == "route")
                            route = value;
                    }
                    break;
                default:
                    reader.Read();
                    if (reader.TokenType is JsonToken.StartObject or JsonToken.StartArray)
                        reader.Skip();
                    break;
            }
        }

        var service = route switch
        {
            "train" => PtService.Train,
            "tram" or "subway" or "light_rail" => PtService.Urban,
            _ => PtService.None,
        };
        if (service == PtService.None)
            return;
        foreach (var wayId in wayIds)
            result[wayId] = result.TryGetValue(wayId, out var existing) ? existing | service : service;
    }
}
