using Microsoft.EntityFrameworkCore;
using OV_DB.Models;
using OVDB_database.Database;
using OVDB_database.Enums;
using OVDB_database.Models;
using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace OV_DB.Services;

public interface IStationVisitService
{
    Task<StationVisit> MarkAsync(int userId, int stationId, StationVisitLevel level, DateTime? localDate, StationVisitSource source, CancellationToken cancellationToken = default);
    Task<StationVisit> DowngradeToStoppedAsync(int userId, int stationId, CancellationToken cancellationToken = default);
    Task<StationVisit> SetDatesAsync(int userId, int stationId, StationVisitDates dates, CancellationToken cancellationToken = default);
    Task<DateTime> LocalDateAtStationAsync(Station station, CancellationToken cancellationToken = default);
    Task<bool> UnmarkAsync(int userId, int stationId, CancellationToken cancellationToken = default);
    Task<StationVisit> GetAsync(int userId, int stationId, CancellationToken cancellationToken = default);
}

/// <summary>
/// The only place a <see cref="StationVisit"/> is created, changed or removed.
/// </summary>
/// <remarks>
/// Every entry point requires a <see cref="StationVisitSource"/>, all of which denote an explicit
/// user action: nothing may mark a station visited on the user's behalf. Inference proposes; the
/// user decides. <c>StationVisitWriteBoundaryTests</c> asserts no other code writes to the table.
/// </remarks>
public class StationVisitService(OVDBDatabaseContext dbContext, ITimezoneService timezoneService) : IStationVisitService
{
    public Task<StationVisit> GetAsync(int userId, int stationId, CancellationToken cancellationToken = default)
    {
        return dbContext.StationVisits
            .SingleOrDefaultAsync(sv => sv.StationId == stationId && sv.UserId == userId, cancellationToken);
    }

    /// <summary>
    /// Marks a station visited, or raises an existing visit to a higher level. Never lowers one and
    /// never moves a date later: re-marking somewhere can only ever add information.
    /// </summary>
    public async Task<StationVisit> MarkAsync(int userId, int stationId, StationVisitLevel level, DateTime? localDate, StationVisitSource source, CancellationToken cancellationToken = default)
    {
        var visit = await GetAsync(userId, stationId, cancellationToken);
        if (visit == null)
        {
            visit = new StationVisit
            {
                StationId = stationId,
                UserId = userId,
                Source = source,
                CreatedOn = DateTime.UtcNow
            };
            dbContext.StationVisits.Add(visit);
        }

        var date = localDate?.Date;

        // Getting on or off implies the train stopped, so entry/exit fills both levels. Each date
        // only ever moves earlier, so confirming an older trip later still improves the record.
        if (date.HasValue)
        {
            if (!visit.FirstStoppedDate.HasValue || date < visit.FirstStoppedDate)
            {
                visit.FirstStoppedDate = date;
                visit.FirstStoppedRouteInstanceId = null;
            }

            if (level == StationVisitLevel.EntryExit
                && (!visit.FirstEntryExitDate.HasValue || date < visit.FirstEntryExitDate))
            {
                visit.FirstEntryExitDate = date;
                visit.FirstEntryExitRouteInstanceId = null;
            }
        }
        else if (level == StationVisitLevel.EntryExit && !visit.FirstEntryExitDate.HasValue
                 && visit.FirstStoppedDate.HasValue)
        {
            // Upgrading a dated stopped-at visit with no date of its own: the only date we know is
            // the one already on the row, and it is a lower bound for the entry/exit too.
            visit.FirstEntryExitDate = visit.FirstStoppedDate;
            visit.FirstEntryExitRouteInstanceId = visit.FirstStoppedRouteInstanceId;
        }

        await dbContext.SaveChangesAsync(cancellationToken);
        return visit;
    }

    /// <summary>
    /// Lowers a visit back to stopped-at, the one correction <see cref="MarkAsync"/> deliberately
    /// will not make. The stop itself is not in doubt — only the claim of having got on or off — so
    /// the stopped date survives and only the entry/exit date and its trip link are dropped.
    /// </summary>
    public async Task<StationVisit> DowngradeToStoppedAsync(int userId, int stationId, CancellationToken cancellationToken = default)
    {
        var visit = await GetAsync(userId, stationId, cancellationToken);
        if (visit == null || !visit.FirstEntryExitDate.HasValue)
        {
            return visit;
        }

        // The entry/exit date is also a valid lower bound for the stop, so keep it if it is earlier
        // rather than throwing away the only date the row has.
        if (!visit.FirstStoppedDate.HasValue || visit.FirstEntryExitDate < visit.FirstStoppedDate)
        {
            visit.FirstStoppedDate = visit.FirstEntryExitDate;
            visit.FirstStoppedRouteInstanceId = visit.FirstEntryExitRouteInstanceId;
        }

        visit.FirstEntryExitDate = null;
        visit.FirstEntryExitRouteInstanceId = null;
        await dbContext.SaveChangesAsync(cancellationToken);
        return visit;
    }

    /// <summary>
    /// Sets both dates to exactly what the user says they are. This is the <b>only</b> path allowed
    /// to move a date later or to clear one: <see cref="MarkAsync"/> deliberately only ever adds
    /// information, which is right for marking but useless for correcting a mistake.
    /// </summary>
    /// <remarks>
    /// It will not create a visit — a date is a fact about a visit that already exists, and the base
    /// requirement is that only an explicit mark brings one into being. Returns null if there is
    /// none.
    /// </remarks>
    /// <exception cref="ArgumentException">A trip was named that the user does not own.</exception>
    public async Task<StationVisit> SetDatesAsync(int userId, int stationId, StationVisitDates dates, CancellationToken cancellationToken = default)
    {
        var visit = await GetAsync(userId, stationId, cancellationToken);
        if (visit == null)
        {
            return null;
        }

        // A named trip decides its own date. Taking the client's word for both invites the pair to
        // disagree, and the trip is the more specific claim of the two.
        var stopped = await ResolveAsync(userId, dates.FirstStoppedRouteInstanceId, dates.FirstStoppedDate, cancellationToken);
        var entryExit = await ResolveAsync(userId, dates.FirstEntryExitRouteInstanceId, dates.FirstEntryExitDate, cancellationToken);

        // Alighting implies stopping, so an entry/exit date that predates the stopped date pulls it
        // back rather than being refused. Same invariant MarkAsync enforces when entry/exit fills
        // both levels.
        if (entryExit.Date.HasValue && (!stopped.Date.HasValue || entryExit.Date < stopped.Date))
        {
            stopped = entryExit;
        }

        visit.FirstStoppedDate = stopped.Date;
        visit.FirstStoppedRouteInstanceId = stopped.RouteInstanceId;
        visit.FirstEntryExitDate = entryExit.Date;
        visit.FirstEntryExitRouteInstanceId = entryExit.RouteInstanceId;

        // Answering the question retires it, however it was answered.
        if (stopped.Date.HasValue || entryExit.Date.HasValue)
        {
            visit.DatingSkipped = false;
        }

        await dbContext.SaveChangesAsync(cancellationToken);
        return visit;
    }

    private async Task<(DateTime? Date, int? RouteInstanceId)> ResolveAsync(int userId, int? routeInstanceId, DateTime? date, CancellationToken cancellationToken)
    {
        if (!routeInstanceId.HasValue)
        {
            return (date?.Date, null);
        }

        var trip = await dbContext.RouteInstances.AsNoTracking()
            .Where(ri => ri.RouteInstanceId == routeInstanceId.Value)
            .Where(ri => ri.Route.RouteMaps.Any(rm => rm.Map.UserId == userId))
            .Select(ri => (DateTime?)ri.Date)
            .SingleOrDefaultAsync(cancellationToken);
        if (trip == null)
        {
            throw new ArgumentException($"Trip {routeInstanceId} is not available to this user.", nameof(routeInstanceId));
        }

        return (trip.Value.Date, routeInstanceId);
    }

    /// <summary>Today where the station is, which is not always today where the server is.</summary>
    public async Task<DateTime> LocalDateAtStationAsync(Station station, CancellationToken cancellationToken = default)
    {
        var local = await timezoneService.ConvertUtcToLocalTimeAsync(DateTime.UtcNow, station.Lattitude, station.Longitude);
        return local.Date;
    }

    /// <summary>
    /// Removes a visit entirely. Deliberately destructive: saying "I have not been here" should not
    /// leave a hidden row asserting a date the user has just disowned. Undo re-marks.
    /// </summary>
    public async Task<bool> UnmarkAsync(int userId, int stationId, CancellationToken cancellationToken = default)
    {
        var visit = await GetAsync(userId, stationId, cancellationToken);
        if (visit == null)
        {
            return false;
        }

        dbContext.StationVisits.Remove(visit);
        await dbContext.SaveChangesAsync(cancellationToken);
        return true;
    }
}
