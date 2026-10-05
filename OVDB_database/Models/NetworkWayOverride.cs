using System;
using System.ComponentModel.DataAnnotations;
using Microsoft.EntityFrameworkCore;

namespace OVDB_database.Models;

public enum NetworkOverrideKind { Excluded = 0, ForceRail = 1, ForceUrbanRail = 2 }

/// <summary>
/// Manual curation of the network, keyed by OSM way id so it survives reassembly (edges are
/// recreated, way ids are stable). Applied during assembly before mode grouping: ForceRail and
/// ForceUrbanRail pin a way's network over the relation-based classification, Excluded drops it.
/// An override boundary is a break node, so overriding one way mid-edge splits the edge there.
/// </summary>
[Index(nameof(RegionId), nameof(OsmWayId), IsUnique = true)]
public class NetworkWayOverride
{
    [Key]
    public long Id { get; set; }
    public int RegionId { get; set; }
    public Region Region { get; set; } = null!;
    public long OsmWayId { get; set; }
    public NetworkOverrideKind Kind { get; set; }
    public DateTime CreatedAt { get; set; }
}
