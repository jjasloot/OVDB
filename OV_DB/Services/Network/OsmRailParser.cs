using System;
using System.Collections.Generic;
using System.IO;
using Newtonsoft.Json;

namespace OV_DB.Services.Network;

public sealed record RailWay(long Id, long[] NodeIds, string Railway, string Usage, string TrafficMode, string Name);

public sealed class OsmRailData
{
    public Dictionary<long, (double Lon, double Lat)> Nodes { get; } = new();
    public List<RailWay> Ways { get; } = new();
    /// <summary>Named stop nodes from the stops fetch, when the data came from that query.</summary>
    public List<(double Lon, double Lat, string Name)> Stops { get; } = new();
    public string OsmTimestamp { get; private set; }

    internal void SetTimestamp(string value) => OsmTimestamp = value;
}

/// <summary>
/// Streaming parser for Overpass JSON (ways with tags, then skeleton nodes; or stop nodes with
/// tags). Hand-rolled over JsonTextReader because a DOM of a 40 MB response is exactly what
/// the NAS must not build.
/// </summary>
public static class OsmRailParser
{
    public static OsmRailData Parse(Stream stream)
    {
        var data = new OsmRailData();
        using var text = new StreamReader(stream);
        using var reader = new JsonTextReader(text);

        while (reader.Read())
        {
            if (reader.TokenType != JsonToken.PropertyName)
                continue;
            switch ((string)reader.Value)
            {
                case "osm3s":
                    reader.Read(); // StartObject
                    while (reader.Read() && reader.TokenType == JsonToken.PropertyName)
                    {
                        var key = (string)reader.Value;
                        var value = reader.ReadAsString();
                        if (key == "timestamp_osm_base")
                            data.SetTimestamp(value);
                    }
                    break;
                case "elements":
                    reader.Read(); // StartArray
                    while (reader.Read() && reader.TokenType == JsonToken.StartObject)
                        ParseElement(reader, data);
                    return data;
            }
        }
        return data;
    }

    private static void ParseElement(JsonTextReader reader, OsmRailData data)
    {
        string type = null;
        long id = 0;
        double lat = 0, lon = 0;
        List<long> nodeIds = null;
        string railway = null, usage = null, trafficMode = null, name = null;

        while (reader.Read() && reader.TokenType != JsonToken.EndObject)
        {
            if (reader.TokenType != JsonToken.PropertyName)
                continue;
            switch ((string)reader.Value)
            {
                case "type":
                    type = reader.ReadAsString();
                    break;
                case "id":
                    reader.Read();
                    id = Convert.ToInt64(reader.Value);
                    break;
                case "lat":
                    lat = reader.ReadAsDouble() ?? 0;
                    break;
                case "lon":
                    lon = reader.ReadAsDouble() ?? 0;
                    break;
                case "nodes":
                    nodeIds = new List<long>(64);
                    reader.Read(); // StartArray
                    while (reader.Read() && reader.TokenType != JsonToken.EndArray)
                        nodeIds.Add(Convert.ToInt64(reader.Value));
                    break;
                case "tags":
                    reader.Read(); // StartObject
                    while (reader.Read() && reader.TokenType == JsonToken.PropertyName)
                    {
                        var key = (string)reader.Value;
                        var value = reader.ReadAsString();
                        switch (key)
                        {
                            case "railway": railway = value; break;
                            case "usage": usage = value; break;
                            case "railway:traffic_mode": trafficMode = value; break;
                            case "name": name = value; break;
                        }
                    }
                    break;
                default:
                    reader.Read();
                    if (reader.TokenType is JsonToken.StartObject or JsonToken.StartArray)
                        reader.Skip();
                    break;
            }
        }

        if (type == "node")
        {
            data.Nodes[id] = (lon, lat);
            if (railway != null)
                data.Stops.Add((lon, lat, name));
        }
        else if (type == "way" && nodeIds is { Count: >= 2 } && railway != null)
        {
            data.Ways.Add(new RailWay(id, nodeIds.ToArray(), railway, usage, trafficMode, name));
        }
    }
}
