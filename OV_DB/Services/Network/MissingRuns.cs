using System;
using System.Collections.Generic;
using System.Linq;
using NetTopologySuite.Geometries;

namespace OV_DB.Services.Network;

/// <summary>An uncovered (part of a) passenger edge, with the endpoints that tie it to its neighbours.</summary>
public sealed record MissingEdge(long Id, string Name, string FromStation, string ToStation, double MissingKm, Coordinate Start, Coordinate End);

/// <summary>A "what to ride next" entry: connected edges sharing a line name and station span.</summary>
public sealed record MissingRunGroup(string Name, string FromStation, string ToStation, double MissingKm, long[] EdgeIds);

/// <summary>
/// Groups uncovered track into rideable stretches. Name and station span alone are not enough:
/// track numbers ("Spoor 6") and unnamed track repeat all over a region, and pooling them makes
/// one entry of scattered slivers whose zoom spans the country. So within each name/span key,
/// only edges that touch (share an endpoint) end up in the same entry.
/// </summary>
public static class MissingRuns
{
    /// <summary>Endpoints closer than this (~1 m) are the same junction node.</summary>
    private const double SameNodeDeg = 0.00001;

    public static List<MissingRunGroup> Group(IReadOnlyList<MissingEdge> edges, double minKm)
    {
        var result = new List<MissingRunGroup>();
        foreach (var keyed in edges.GroupBy(e => (Name: e.Name ?? "", e.FromStation, e.ToStation)))
        {
            var members = keyed.ToList();
            var parent = Enumerable.Range(0, members.Count).ToArray();
            int Find(int i) => parent[i] == i ? i : parent[i] = Find(parent[i]);

            // Union edges that share a node; snap endpoints to a grid cell so lookups are O(1).
            var byNode = new Dictionary<(long, long), int>();
            for (var i = 0; i < members.Count; i++)
            {
                foreach (var c in new[] { members[i].Start, members[i].End })
                {
                    var cell = ((long)Math.Round(c.X / SameNodeDeg), (long)Math.Round(c.Y / SameNodeDeg));
                    if (byNode.TryGetValue(cell, out var other))
                        parent[Find(i)] = Find(other);
                    else
                        byNode[cell] = i;
                }
            }

            foreach (var component in Enumerable.Range(0, members.Count).GroupBy(Find))
            {
                var parts = component.Select(i => members[i]).ToList();
                var km = Math.Round(parts.Sum(p => p.MissingKm), 2);
                if (km >= minKm)
                    result.Add(new MissingRunGroup(keyed.Key.Name, keyed.Key.FromStation, keyed.Key.ToStation, km, parts.Select(p => p.Id).ToArray()));
            }
        }
        return result.OrderByDescending(r => r.MissingKm).ToList();
    }
}
