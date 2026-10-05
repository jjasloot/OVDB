using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using Microsoft.EntityFrameworkCore;

namespace OVDB_database.Models;

/// <summary>
/// Two networks only. The tram/light-rail/metro distinction is hopeless both in OSM tagging
/// and in user perception (Ruhrgebiet Stadtbahn is all three at once), so everything urban is
/// one network.
/// </summary>
public enum NetworkMode { Rail = 0, UrbanRail = 1 }

public enum NetworkStatus { Queued = 0, Assembling = 1, Ready = 2, Failed = 3 }

/// <summary>
/// The assembled rail network of one region and mode: the passenger-service track as edges,
/// against which the user's routes are matched for completion. See docs/region-network-completion.md.
/// </summary>
[Index(nameof(RegionId), nameof(Mode), IsUnique = true)]
public class RegionNetwork
{
    [Key]
    public int Id { get; set; }
    public int RegionId { get; set; }
    public Region Region { get; set; } = null!;
    public NetworkMode Mode { get; set; }
    public NetworkStatus Status { get; set; }
    public DateTime? AssembledAt { get; set; }
    /// <summary>osm3s.timestamp_osm_base of the Overpass response, for display and reproducibility.</summary>
    public string? OsmDataTimestamp { get; set; }
    public int EdgeCount { get; set; }
    /// <summary>
    /// Bumped on every reassembly; the ETag of the geometry payload. App-managed on purpose:
    /// MariaDB has no rowversion type, and an auto-updating timestamp would change on any row
    /// touch rather than only when the geometry did.
    /// </summary>
    public int Version { get; set; }
    /// <summary>Passenger-service length: the completion denominator.</summary>
    public double TotalKm { get; set; }
    /// <summary>Non-passenger length (freight, unserved museum lines, depots). Shown, never in the denominator.</summary>
    public double BonusKm { get; set; }
    /// <summary>False when the region had too few route relations and the tag heuristic classified the track.</summary>
    public bool ClassifiedByRelations { get; set; }
    public string? Error { get; set; }
    public List<NetworkEdge> Edges { get; set; } = new();
}
