using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using OVDB_database.Database;
using OVDB_database.Models;
using OV_DB.Services;

namespace OV_DB.Services.Network;

public abstract record NetworkMatchWorkItem;
/// <summary>A route was created, its geometry or type changed: rematch it against every network of its regions.</summary>
public sealed record RouteChanged(int RouteId) : NetworkMatchWorkItem;
/// <summary>A network was (re)assembled: rematch every eligible route of its region against it.</summary>
public sealed record NetworkChanged(int RegionNetworkId) : NetworkMatchWorkItem;

public interface INetworkMatchIndexCache
{
    Task<NetworkMatchIndex> GetAsync(OVDBDatabaseContext db, RegionNetwork network, CancellationToken cancellationToken);
    void Invalidate(int networkId);
}

/// <summary>
/// Match indexes keyed by network id and version: a route save must not rebuild a 137k-sample
/// index, and a reassembly (which bumps the version) must not reuse the old one. Only completed
/// indexes are cached — never a task bound to a caller's scoped DbContext — and, like
/// <see cref="MatcherIndexCache"/>, entries idle for half an hour are dropped: the work is
/// bursty (an assembly, then nothing for days) and resident indexes cost tens of MB each on the NAS.
/// </summary>
public sealed class NetworkMatchIndexCache : INetworkMatchIndexCache, IDisposable
{
    private static readonly TimeSpan IdleTimeout = TimeSpan.FromMinutes(30);

    private sealed class Entry
    {
        public int Version;
        public NetworkMatchIndex Index;
        public DateTime LastUsedUtc;
    }

    private readonly ConcurrentDictionary<int, Entry> _entries = new();
    private readonly Timer _sweep;

    public NetworkMatchIndexCache()
    {
        _sweep = new Timer(_ => DropIdle(), null, IdleTimeout, IdleTimeout);
    }

    public async Task<NetworkMatchIndex> GetAsync(OVDBDatabaseContext db, RegionNetwork network, CancellationToken cancellationToken)
    {
        if (_entries.TryGetValue(network.Id, out var entry) && entry.Version == network.Version)
        {
            entry.LastUsedUtc = DateTime.UtcNow;
            return entry.Index;
        }

        // Two callers racing here both build; the queue is single-threaded and route saves are
        // rare enough that a duplicate build beats holding a lock across a database read.
        var index = await BuildAsync(db, network, cancellationToken);
        _entries[network.Id] = new Entry { Version = network.Version, Index = index, LastUsedUtc = DateTime.UtcNow };
        return index;
    }

    public void Invalidate(int networkId) => _entries.TryRemove(networkId, out _);

    private void DropIdle()
    {
        var cutoff = DateTime.UtcNow - IdleTimeout;
        foreach (var (id, entry) in _entries)
            if (entry.LastUsedUtc < cutoff)
                _entries.TryRemove(id, out _);
    }

    public void Dispose() => _sweep.Dispose();

    private static async Task<NetworkMatchIndex> BuildAsync(OVDBDatabaseContext db, RegionNetwork network, CancellationToken cancellationToken)
    {
        var edges = await db.NetworkEdges
            .Where(e => e.RegionNetworkId == network.Id)
            .Select(e => new MatchableEdge(e.Id, e.Geometry, e.SampleCount, e.HiddenTwins))
            .ToListAsync(cancellationToken);
        var lat0 = await db.Regions.Where(r => r.Id == network.RegionId).Select(r => r.Geometry.Centroid.Y).FirstAsync(cancellationToken);
        return new NetworkMatchIndex(edges, new NetworkGeo.LocalProjection(lat0));
    }
}

public interface INetworkMatchService
{
    Task MatchRouteAsync(int routeId, CancellationToken cancellationToken = default);
    Task RematchNetworkAsync(int regionNetworkId, Func<int, Task> progress = null, CancellationToken cancellationToken = default);
}

/// <summary>
/// Writes <see cref="RouteNetworkEdge"/> rows: per route on change, per network after assembly.
/// Eligibility is the OVDB-native <c>RouteType.MatchesRailNetworks</c>; a route of any
/// rail-bound type matches every network of its regions (geometry decides what it covers).
/// </summary>
public class NetworkMatchService(OVDBDatabaseContext db, INetworkMatchIndexCache indexCache, ILogger<NetworkMatchService> logger) : INetworkMatchService
{
    private const int RoutePageSize = 200;

    public async Task MatchRouteAsync(int routeId, CancellationToken cancellationToken = default)
    {
        var route = await db.Routes
            .Where(r => r.RouteId == routeId)
            .Select(r => new
            {
                r.RouteId,
                r.LineString,
                Eligible = r.RouteType != null && r.RouteType.MatchesRailNetworks,
                RegionIds = r.Regions.Select(reg => reg.Id).ToList(),
            })
            .FirstOrDefaultAsync(cancellationToken);

        await db.RouteNetworkEdges.Where(m => m.RouteId == routeId).ExecuteDeleteAsync(cancellationToken);
        if (route == null || !route.Eligible || route.LineString == null || route.RegionIds.Count == 0)
            return;

        var networks = await db.RegionNetworks
            .Where(n => n.Status == NetworkStatus.Ready && route.RegionIds.Contains(n.RegionId))
            .ToListAsync(cancellationToken);
        var rows = new List<RouteNetworkEdge>();
        foreach (var network in networks)
        {
            var index = await indexCache.GetAsync(db, network, cancellationToken);
            foreach (var (edgeId, bits) in index.Match(route.LineString))
                rows.Add(ToRow(route.RouteId, edgeId, bits));
        }
        if (rows.Count > 0)
        {
            db.RouteNetworkEdges.AddRange(rows);
            await db.SaveChangesAsync(cancellationToken);
            db.ChangeTracker.Clear();
        }
    }

    public async Task RematchNetworkAsync(int regionNetworkId, Func<int, Task> progress = null, CancellationToken cancellationToken = default)
    {
        var network = await db.RegionNetworks.FirstOrDefaultAsync(n => n.Id == regionNetworkId, cancellationToken);
        if (network == null || network.Status != NetworkStatus.Ready)
            return;

        // Reassembly already cascaded the old rows away; a standalone rematch (after a constant
        // change, say) must clear them itself.
        await db.RouteNetworkEdges.Where(m => m.Edge.RegionNetworkId == network.Id).ExecuteDeleteAsync(cancellationToken);
        indexCache.Invalidate(network.Id);
        var index = await indexCache.GetAsync(db, network, cancellationToken);

        var routeIds = await db.Routes
            .Where(r => r.Regions.Any(reg => reg.Id == network.RegionId) && r.RouteType != null && r.RouteType.MatchesRailNetworks && r.LineString != null)
            .OrderBy(r => r.RouteId)
            .Select(r => r.RouteId)
            .ToListAsync(cancellationToken);

        var done = 0;
        var matched = 0;
        foreach (var page in routeIds.Chunk(RoutePageSize))
        {
            // Geometry is paged: all routes at once is hundreds of MB (see MatcherIndex).
            var routes = await db.Routes
                .Where(r => page.Contains(r.RouteId))
                .Select(r => new { r.RouteId, r.LineString })
                .ToListAsync(cancellationToken);
            var rows = new List<RouteNetworkEdge>();
            foreach (var route in routes)
            {
                foreach (var (edgeId, bits) in index.Match(route.LineString))
                    rows.Add(ToRow(route.RouteId, edgeId, bits));
            }
            if (rows.Count > 0)
            {
                db.RouteNetworkEdges.AddRange(rows);
                await db.SaveChangesAsync(cancellationToken);
                db.ChangeTracker.Clear();
                matched += rows.Count;
            }
            done += page.Length;
            if (progress != null)
                await progress(routeIds.Count == 0 ? 100 : done * 100 / routeIds.Count);
        }
        logger.LogInformation("Rematched network {NetworkId} ({Mode}): {Routes} routes, {Rows} match rows", network.Id, network.Mode, routeIds.Count, matched);
    }

    private static RouteNetworkEdge ToRow(int routeId, long edgeId, bool[] bits) => new()
    {
        RouteId = routeId,
        NetworkEdgeId = edgeId,
        CoveredSamples = NetworkLattice.ToBytes(bits),
        CoveredCount = NetworkLattice.PopCount(bits),
    };
}
