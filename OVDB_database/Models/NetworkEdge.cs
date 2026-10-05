using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using NetTopologySuite.Geometries;

namespace OVDB_database.Models;

/// <summary>
/// One stretch of deduplicated centerline between two break points (junction, passenger-flag
/// change, override boundary, station), capped at 10 km. A container for geometry, a name and a
/// station span — coverage itself lives on the 25 m sample lattice, never on the edge as a whole.
/// </summary>
public class NetworkEdge
{
    [Key]
    public long Id { get; set; }
    public int RegionNetworkId { get; set; }
    public RegionNetwork Network { get; set; } = null!;
    /// <summary>Full centerline, the matching input.</summary>
    public LineString Geometry { get; set; } = null!;
    /// <summary>
    /// Douglas-Peucker ~10 m, for display. Keeps a subset of Geometry's own vertices, which is
    /// what lets completion intervals be mapped onto it exactly.
    /// </summary>
    public LineString SimplifiedGeometry { get; set; } = null!;
    /// <summary>
    /// Physical tracks hidden by the parallel-track dedup, attached to this surviving edge. The
    /// matcher probes them so riding either track of a corridor covers the corridor. Never displayed.
    /// </summary>
    public MultiLineString? HiddenTwins { get; set; }
    /// <summary>Geodesic length, no calibration factor.</summary>
    public double LengthKm { get; set; }
    /// <summary>
    /// Number of samples on the uniform 25 m chainage lattice over Geometry — computed once at
    /// assembly by the same function the matcher samples with, and never recomputed: the stored
    /// bitmaps and the completion intervals index into this lattice bit for bit.
    /// </summary>
    public int SampleCount { get; set; }
    /// <summary>Passenger-service track (a route relation runs over it, or the tag heuristic says so).</summary>
    public bool IsPassenger { get; set; }
    /// <summary>Dominant OSM name along the edge.</summary>
    public string? Name { get; set; }
    /// <summary>Station at which the edge starts, when it was split there.</summary>
    public string? FromStation { get; set; }
    /// <summary>Station at which the edge ends, when it was split there.</summary>
    public string? ToStation { get; set; }
    /// <summary>JSON array of the OSM way ids the edge was built from: the curation key.</summary>
    public string OsmWayIds { get; set; } = "[]";
    public List<RouteNetworkEdge> Matches { get; set; } = new();
}
