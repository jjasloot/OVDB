using Microsoft.EntityFrameworkCore;
using OV_DB.Models;
using OVDB_database.Database;
using OVDB_database.Enums;
using OVDB_database.Models;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace OV_DB.Services;

public interface IStationVisitService
{
    Task<StationVisit> MarkAsync(int userId, int stationId, StationVisitLevel level, DateTime? localDate, StationVisitSource source, CancellationToken cancellationToken = default);
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
