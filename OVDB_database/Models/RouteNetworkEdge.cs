using Microsoft.EntityFrameworkCore;

namespace OVDB_database.Models;

/// <summary>
/// A route's coverage of one network edge: one bit per sample of the edge's lattice, set where
/// the route passes within tolerance of the sample or of a hidden-twin probe. Completion ORs
/// these across all of a user's instanced routes, so rides logged in legs and arrive/depart
/// pairs at a station combine into one covered stretch.
/// </summary>
[PrimaryKey(nameof(RouteId), nameof(NetworkEdgeId))]
[Index(nameof(NetworkEdgeId))]
public class RouteNetworkEdge
{
    public int RouteId { get; set; }
    public Route Route { get; set; } = null!;
    public long NetworkEdgeId { get; set; }
    public NetworkEdge Edge { get; set; } = null!;
    /// <summary>
    /// Little-endian bit per sample, length ceil(SampleCount / 8) — at most 51 bytes for a 10 km
    /// edge. A row exists only when at least four samples (~100 m) are set, so a perpendicular
    /// crossing stores nothing.
    /// </summary>
    public byte[] CoveredSamples { get; set; } = [];
    /// <summary>Popcount of CoveredSamples, for cheap aggregation.</summary>
    public int CoveredCount { get; set; }
}
