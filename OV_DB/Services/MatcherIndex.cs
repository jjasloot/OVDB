using NetTopologySuite.Index.Strtree;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace OV_DB.Services;

/// <summary>
/// One segment of a route's simplified geometry, in degrees. A segment rather than a whole route
/// because route envelopes are useless as an index: a cross-country line's bounding box covers half
/// the stations in the country, so every query would return nearly everything.
/// </summary>
public readonly record struct RouteSegment(int RouteId, double X1, double Y1, double X2, double Y2);

/// <summary>A station reduced to what the index needs.</summary>
public readonly record struct StationPoint(int StationId, double Longitude, double Lattitude);

/// <summary>
/// The spatial half of the matcher: every route broken into segments, and every active station as a
/// point. Built once and shared, because building it is the expensive part.
/// </summary>
/// <remarks>
/// Measured against production data: 12,809 routes hold 9.9M coordinates and 382 MB if kept whole.
/// Simplified to 50 m they are 476k segments and 65 MB, which changes what the 300 m threshold finds
/// by about 1% — the geometry tier is the weakest evidence anyway, and every result is a proposal a
/// human confirms. Indexing is global rather than per user; results are scoped to the caller's maps
/// by the database query that turns route ids into trips, so an unowned route simply yields nothing.
/// </remarks>
public sealed class MatcherIndex
{
    public required STRtree<RouteSegment> Segments { get; init; }
    public required STRtree<StationPoint> Stations { get; init; }
    /// <summary>First and last coordinate of each route, for the "you started or ended here" test.</summary>
    public required IReadOnlyDictionary<int, (double X1, double Y1, double X2, double Y2)> RouteEndpoints { get; init; }
}

public interface IMatcherIndexCache
{
    Task<MatcherIndex> GetAsync(Func<CancellationToken, Task<MatcherIndex>> build, CancellationToken cancellationToken = default);
    void Invalidate();
}

/// <summary>
/// Holds the index between requests and drops it once nobody is using it.
/// </summary>
/// <remarks>
/// The work this serves is bursty — a backfill session hammers it, then nothing touches it for days
/// — so keeping 65 MB resident forever to save a seven second rebuild is a bad trade on a machine
/// this modest. An idle timeout gets both: warm within a session, reclaimed between them.
/// </remarks>
public sealed class MatcherIndexCache : IMatcherIndexCache, IDisposable
{
    private static readonly TimeSpan IdleTimeout = TimeSpan.FromMinutes(30);

    private readonly SemaphoreSlim _gate = new(1, 1);
    private readonly Timer _sweep;
    private MatcherIndex _index;
    private DateTime _lastUsedUtc;

    public MatcherIndexCache()
    {
        _sweep = new Timer(_ => DropIfIdle(), null, IdleTimeout, IdleTimeout);
    }

    public async Task<MatcherIndex> GetAsync(Func<CancellationToken, Task<MatcherIndex>> build, CancellationToken cancellationToken = default)
    {
        // Under the gate so a burst of concurrent callers pays the build cost once, not once each.
        await _gate.WaitAsync(cancellationToken);
        try
        {
            _index ??= await build(cancellationToken);
            _lastUsedUtc = DateTime.UtcNow;
            return _index;
        }
        finally
        {
            _gate.Release();
        }
    }

    public void Invalidate()
    {
        if (!_gate.Wait(TimeSpan.FromSeconds(5)))
        {
            // A build is in flight. It will produce a fresh index anyway, so dropping is pointless
            // and blocking an import on it would be worse.
            return;
        }
        try
        {
            _index = null;
        }
        finally
        {
            _gate.Release();
        }
    }

    private void DropIfIdle()
    {
        if (!_gate.Wait(TimeSpan.Zero))
        {
            return;
        }
        try
        {
            if (_index != null && DateTime.UtcNow - _lastUsedUtc >= IdleTimeout)
            {
                _index = null;
            }
        }
        finally
        {
            _gate.Release();
        }
    }

    public void Dispose()
    {
        _sweep.Dispose();
        _gate.Dispose();
    }
}
