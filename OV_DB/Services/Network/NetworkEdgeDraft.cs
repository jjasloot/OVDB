using System.Collections.Generic;
using NetTopologySuite.Geometries;
using OVDB_database.Models;

namespace OV_DB.Services.Network;

/// <summary>An edge while the assembly pipeline is still cutting and merging; becomes a <see cref="NetworkEdge"/> at persistence.</summary>
public sealed class NetworkEdgeDraft
{
    public NetworkMode Mode { get; set; }
    public LineString Geometry { get; set; }
    public double LengthKm { get; set; }
    public bool IsPassenger { get; set; }
    public string Name { get; set; }
    public string FromStation { get; set; }
    public string ToStation { get; set; }
    public List<long> OsmWayIds { get; set; } = new();
    /// <summary>Physical track geometries hidden by the parallel dedup and attached to this survivor.</summary>
    public List<Coordinate[]> HiddenTwins { get; set; }

    public NetworkEdgeDraft CopyMetadataTo(NetworkEdgeDraft target)
    {
        target.Mode = Mode;
        target.IsPassenger = IsPassenger;
        target.Name = Name;
        target.OsmWayIds = OsmWayIds;
        return target;
    }
}
