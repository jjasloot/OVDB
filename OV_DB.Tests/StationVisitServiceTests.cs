using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using OV_DB.Models;
using OV_DB.Services;
using OVDB_database.Database;
using OVDB_database.Enums;
using OVDB_database.Models;

namespace OV_DB.Tests
{
    // Marking is the one place user intent enters the system, so its rules are worth pinning:
    // a mark only ever adds information, an un-mark really removes the row, and the weaker
    // "stopped" claim is what a plain tap records.
    public class StationVisitServiceTests
    {
        private static OVDBDatabaseContext NewContext()
        {
            var options = new DbContextOptionsBuilder<OVDBDatabaseContext>()
                .UseInMemoryDatabase(Guid.NewGuid().ToString())
                .Options;
            return new OVDBDatabaseContext(options);
        }

        private static StationVisitService NewService(OVDBDatabaseContext context) =>
            new(context, new StubTimezoneService());

        private static readonly DateTime Day = new(2026, 5, 3);
        private static readonly DateTime Earlier = new(2019, 4, 1);

        [Fact]
        public async Task Mark_CreatesAStoppedVisit()
        {
            using var context = NewContext();
            var service = NewService(context);

            await service.MarkAsync(1, 10, StationVisitLevel.Stopped, Day, StationVisitSource.Telegram);

            var visit = await context.StationVisits.SingleAsync();
            Assert.Equal(Day, visit.FirstStoppedDate);
            Assert.Null(visit.FirstEntryExitDate);
            Assert.Equal(StationVisitSource.Telegram, visit.Source);
            Assert.NotNull(visit.CreatedOn);
        }

        [Fact]
        public async Task Mark_WithoutADateLeavesTheVisitUndated()
        {
            using var context = NewContext();
            var service = NewService(context);

            // The web map marks without claiming a date; the visit is valid and joins the backfill queue.
            await service.MarkAsync(1, 10, StationVisitLevel.Stopped, null, StationVisitSource.Web);

            var visit = await context.StationVisits.SingleAsync();
            Assert.Null(visit.FirstStoppedDate);
            Assert.Null(visit.FirstEntryExitDate);
        }

        [Fact]
        public async Task Mark_EntryExitFillsBothLevels()
        {
            using var context = NewContext();
            var service = NewService(context);

            // Getting on or off implies the train stopped.
            await service.MarkAsync(1, 10, StationVisitLevel.EntryExit, Day, StationVisitSource.Web);

            var visit = await context.StationVisits.SingleAsync();
            Assert.Equal(Day, visit.FirstStoppedDate);
            Assert.Equal(Day, visit.FirstEntryExitDate);
        }

        [Fact]
        public async Task Mark_UpgradingAStoppedVisitKeepsItsDateAsTheEntryExitDate()
        {
            using var context = NewContext();
            var service = NewService(context);
            await service.MarkAsync(1, 10, StationVisitLevel.Stopped, Day, StationVisitSource.Telegram);

            // The "entry/exit" button carries no new date: the only one known is already on the row.
            await service.MarkAsync(1, 10, StationVisitLevel.EntryExit, null, StationVisitSource.Telegram);

            var visit = await context.StationVisits.SingleAsync();
            Assert.Equal(Day, visit.FirstStoppedDate);
            Assert.Equal(Day, visit.FirstEntryExitDate);
        }

        [Fact]
        public async Task Mark_NeverMovesADateLater()
        {
            using var context = NewContext();
            var service = NewService(context);
            await service.MarkAsync(1, 10, StationVisitLevel.EntryExit, Earlier, StationVisitSource.Backfill);

            await service.MarkAsync(1, 10, StationVisitLevel.EntryExit, Day, StationVisitSource.Web);

            var visit = await context.StationVisits.SingleAsync();
            Assert.Equal(Earlier, visit.FirstStoppedDate);
            Assert.Equal(Earlier, visit.FirstEntryExitDate);
        }

        [Fact]
        public async Task Mark_AnEarlierTripImprovesTheRecord()
        {
            using var context = NewContext();
            var service = NewService(context);
            await service.MarkAsync(1, 10, StationVisitLevel.EntryExit, Day, StationVisitSource.Web);

            await service.MarkAsync(1, 10, StationVisitLevel.EntryExit, Earlier, StationVisitSource.Backfill);

            var visit = await context.StationVisits.SingleAsync();
            Assert.Equal(Earlier, visit.FirstEntryExitDate);
        }

        [Fact]
        public async Task Mark_NeverLowersTheLevel()
        {
            using var context = NewContext();
            var service = NewService(context);
            await service.MarkAsync(1, 10, StationVisitLevel.EntryExit, Day, StationVisitSource.Web);

            // A later plain tap must not demote an established entry/exit to merely stopped.
            await service.MarkAsync(1, 10, StationVisitLevel.Stopped, Day, StationVisitSource.Web);

            var visit = await context.StationVisits.SingleAsync();
            Assert.Equal(Day, visit.FirstEntryExitDate);
        }

        [Fact]
        public async Task Mark_IsIdempotent()
        {
            using var context = NewContext();
            var service = NewService(context);

            await service.MarkAsync(1, 10, StationVisitLevel.Stopped, Day, StationVisitSource.Web);
            await service.MarkAsync(1, 10, StationVisitLevel.Stopped, Day, StationVisitSource.Web);

            Assert.Equal(1, await context.StationVisits.CountAsync());
        }

        [Fact]
        public async Task Unmark_RemovesTheRowEntirely()
        {
            using var context = NewContext();
            var service = NewService(context);
            await service.MarkAsync(1, 10, StationVisitLevel.EntryExit, Day, StationVisitSource.Web);

            var removed = await service.UnmarkAsync(1, 10);

            Assert.True(removed);
            Assert.Empty(context.StationVisits);
        }

        [Fact]
        public async Task Unmark_ThenMarkAgainStartsUndated()
        {
            using var context = NewContext();
            var service = NewService(context);
            await service.MarkAsync(1, 10, StationVisitLevel.EntryExit, Day, StationVisitSource.Web);
            await service.UnmarkAsync(1, 10);

            // Losing the dates is the accepted trade for not keeping a row that asserts a visit the
            // user has disowned; the backfill can date it again.
            await service.MarkAsync(1, 10, StationVisitLevel.Stopped, null, StationVisitSource.Web);

            var visit = await context.StationVisits.SingleAsync();
            Assert.Null(visit.FirstStoppedDate);
            Assert.Null(visit.FirstEntryExitDate);
        }

        [Fact]
        public async Task Unmark_OnAnUnvisitedStationIsANoOp()
        {
            using var context = NewContext();
            var service = NewService(context);

            Assert.False(await service.UnmarkAsync(1, 10));
        }

        [Fact]
        public async Task Mark_KeepsUsersApart()
        {
            using var context = NewContext();
            var service = NewService(context);

            await service.MarkAsync(1, 10, StationVisitLevel.EntryExit, Day, StationVisitSource.Web);
            await service.MarkAsync(2, 10, StationVisitLevel.Stopped, Day, StationVisitSource.Web);
            await service.UnmarkAsync(1, 10);

            var remaining = await context.StationVisits.SingleAsync();
            Assert.Equal(2, remaining.UserId);
        }

        private sealed class StubTimezoneService : ITimezoneService
        {
            public Task<DateTime> ConvertUtcToLocalTimeAsync(DateTime utcDateTime, double latitude, double longitude)
                => Task.FromResult(utcDateTime);

            public double CalculateDurationInHours(DateTime startTime, DateTime endTime, NetTopologySuite.Geometries.LineString lineString)
                => 0;
        }
    }
}
