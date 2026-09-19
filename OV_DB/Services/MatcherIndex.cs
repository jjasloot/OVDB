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
/// Every route broken into segments, plus where each one begins and ends. Built once and shared,
/// because building it is the expensive part.
/// </summary>
/// <remarks>
/// Measured against production data: 12,809 routes hold 9.9M coordinates and 382 MB if kept whole.
/// Simplified to 50 m they are 476k segments and 65 MB, which changes what the 300 m threshold finds
/// by about 1% — the geometry tier is the weakest evidence anyway, and every result is a proposal a
/// human confirms. Indexing is global rather than per user; results are scoped to the caller's maps
/// by the database query that turns route ids into trips, so an unowned route simply yields nothing.
/// </remarks>
public sealed class RouteIndex
{
    public required STRtree<RouteSegment> Segments { get; init; }
    /// <summary>First and last coordinate of each route, for the "you started or ended here" test.</summary>
    public required IReadOnlyDictionary<int, (double X1, double Y1, double X2, double Y2)> Endpoints { get; init; }
    /// <summary>
    /// What the index was built from. Compared on every read so a route imported a minute ago cannot
    /// be invisible until the cache happens to expire — which it was, because nothing remembered to
    /// invalidate. A counted fingerprint needs nobody to remember anything.
    /// </summary>
    public required int RouteCount { get; init; }
}

/// <summary>
/// Every active station as a point. Kept apart from <see cref="RouteIndex"/> deliberately: reading
/// it costs one small non-spatial query, while route geometry is hundreds of megabytes, and the two
/// go stale for entirely unrelated reasons. Sharing one fingerprint meant importing a route — which
/// every Träwelling trip on a new line does — threw away the station index too and rebuilt all of
/// that geometry inside the save request, for a lookup that never touches a route.
/// </summary>
public sealed class StationIndex
{
    public required STRtree<StationPoint> Stations { get; init; }
    /// <summary>See <see cref="RouteIndex.RouteCount"/>: the same trick, counted separately.</summary>
    public required int StationCount { get; init; }
}

public interface IMatcherIndexCache
{
    /// <summary>
    /// The cached route index if it was built from this same count, otherwise a freshly built one.
    /// </summary>
    Task<RouteIndex> GetRoutesAsync(int routeCount, Func<CancellationToken, Task<RouteIndex>> build, CancellationToken cancellationToken = default);
    /// <summary>The same, for stations.</summary>
    Task<StationIndex> GetStationsAsync(int stationCount, Func<CancellationToken, Task<StationIndex>> build, CancellationToken cancellationToken = default);
    void Invalidate();
}

/// <summary>
/// Holds the indexes between requests and drops them once nobody is using them.
/// </summary>
/// <remarks>
/// The work this serves is bursty — a backfill session hammers it, then nothing touches it for days
/// — so keeping 65 MB resident forever to save a seven second rebuild is a bad trade on a machine
/// this modest. An idle timeout gets both: warm within a session, reclaimed between them.
/// </remarks>
public sealed class MatcherIndexCache : IMatcherIndexCache, IDisposable
{
    private static readonly TimeSpan IdleTimeout = TimeSpan.FromMinutes(30);

    private readonly Slot<RouteIndex> _routes = new(index => index.RouteCount);
    private readonly Slot<StationIndex> _stations = new(index => index.StationCount);
    private readonly Timer _sweep;

    public MatcherIndexCache()
    {
        _sweep = new Timer(_ => DropIfIdle(), null, IdleTimeout, IdleTimeout);
    }

    public Task<RouteIndex> GetRoutesAsync(int routeCount, Func<CancellationToken, Task<RouteIndex>> build, CancellationToken cancellationToken = default) =>
        _routes.GetAsync(routeCount, build, cancellationToken);

    public Task<StationIndex> GetStationsAsync(int stationCount, Func<CancellationToken, Task<StationIndex>> build, CancellationToken cancellationToken = default) =>
        _stations.GetAsync(stationCount, build, cancellationToken);

    public void Invalidate()
    {
        _routes.Invalidate();
        _stations.Invalidate();
    }

    private void DropIfIdle()
    {
        _routes.DropIfIdle(IdleTimeout);
        _stations.DropIfIdle(IdleTimeout);
    }

    public void Dispose()
    {
        _sweep.Dispose();
        _routes.Dispose();
        _stations.Dispose();
    }

    /// <summary>One cached index and the gate that keeps a burst of callers from all building it.</summary>
    private sealed class Slot<T>(Func<T, int> fingerprint) : IDisposable where T : class
    {
        private readonly SemaphoreSlim _gate = new(1, 1);
        private T _index;
        private DateTime _lastUsedUtc;

        public async Task<T> GetAsync(int count, Func<CancellationToken, Task<T>> build, CancellationToken cancellationToken)
        {
            // Under the gate so a burst of concurrent callers pays the build cost once, not once each.
            await _gate.WaitAsync(cancellationToken);
            try
            {
                if (_index != null && fingerprint(_index) != count)
                {
                    // Something was imported or removed since this was built, so it is answering
                    // about a world that no longer exists.
                    _index = null;
                }
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
                // A build is in flight. It will produce a fresh index anyway, so dropping is
                // pointless and blocking an import on it would be worse.
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

        public void DropIfIdle(TimeSpan idleTimeout)
        {
            if (!_gate.Wait(TimeSpan.Zero))
            {
                return;
            }
            try
            {
                if (_index != null && DateTime.UtcNow - _lastUsedUtc >= idleTimeout)
                {
                    _index = null;
                }
            }
            finally
            {
                _gate.Release();
            }
        }

        public void Dispose() => _gate.Dispose();
    }
}
