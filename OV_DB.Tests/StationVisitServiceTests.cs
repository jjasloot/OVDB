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
        public async Task Downgrade_DropsTheEntryExitClaimButKeepsTheStop()
        {
            using var context = NewContext();
            var service = NewService(context);
            await service.MarkAsync(1, 10, StationVisitLevel.Stopped, Earlier, StationVisitSource.Backfill);
            await service.MarkAsync(1, 10, StationVisitLevel.EntryExit, Day, StationVisitSource.Web);

            // "Actually I only passed through" — the stop is not in doubt, only the getting off.
            await service.DowngradeToStoppedAsync(1, 10);

            var visit = await context.StationVisits.SingleAsync();
            Assert.Equal(Earlier, visit.FirstStoppedDate);
            Assert.Null(visit.FirstEntryExitDate);
        }

        [Fact]
        public async Task Downgrade_KeepsTheEntryExitDateWhenItIsTheOnlyOneKnown()
        {
            using var context = NewContext();
            var service = NewService(context);
            await service.MarkAsync(1, 10, StationVisitLevel.EntryExit, Day, StationVisitSource.Web);
            var visit = await context.StationVisits.SingleAsync();
            visit.FirstStoppedDate = null;
            await context.SaveChangesAsync();

            await service.DowngradeToStoppedAsync(1, 10);

            // Lowering the level must not silently throw away the date the row was carrying.
            visit = await context.StationVisits.SingleAsync();
            Assert.Equal(Day, visit.FirstStoppedDate);
            Assert.Null(visit.FirstEntryExitDate);
        }

        [Fact]
        public async Task Downgrade_OnAStoppedVisitChangesNothing()
        {
            using var context = NewContext();
            var service = NewService(context);
            await service.MarkAsync(1, 10, StationVisitLevel.Stopped, Day, StationVisitSource.Web);

            await service.DowngradeToStoppedAsync(1, 10);

            var visit = await context.StationVisits.SingleAsync();
            Assert.Equal(Day, visit.FirstStoppedDate);
            Assert.Null(visit.FirstEntryExitDate);
        }

        [Fact]
        public async Task Downgrade_NeverCreatesAVisit()
        {
            using var context = NewContext();
            var service = NewService(context);

            // Nothing may mark a station visited as a side effect, least of all a lowering.
            Assert.Null(await service.DowngradeToStoppedAsync(1, 10));
            Assert.Empty(context.StationVisits);
        }

        [Fact]
        public async Task SetDates_MovesADateLater()
        {
            using var context = NewContext();
            var service = NewService(context);
            await service.MarkAsync(1, 10, StationVisitLevel.Stopped, Earlier, StationVisitSource.Web);

            // The whole point of the edit path: MarkAsync refuses this, and a correction needs it.
            await service.SetDatesAsync(1, 10, new StationVisitDates { FirstStoppedDate = Day });

            var visit = await context.StationVisits.SingleAsync();
            Assert.Equal(Day, visit.FirstStoppedDate);
        }

        [Fact]
        public async Task SetDates_ClearsADate()
        {
            using var context = NewContext();
            var service = NewService(context);
            await service.MarkAsync(1, 10, StationVisitLevel.EntryExit, Day, StationVisitSource.Web);

            await service.SetDatesAsync(1, 10, new StationVisitDates());

            // Still visited, just no longer claiming to know when.
            var visit = await context.StationVisits.SingleAsync();
            Assert.Null(visit.FirstStoppedDate);
            Assert.Null(visit.FirstEntryExitDate);
        }

        [Fact]
        public async Task SetDates_PullsTheStoppedDateBackToMeetAnEarlierEntryExit()
        {
            using var context = NewContext();
            var service = NewService(context);
            await service.MarkAsync(1, 10, StationVisitLevel.Stopped, Day, StationVisitSource.Web);

            // Alighting implies stopping, so this is a correction to accept, not to refuse.
            await service.SetDatesAsync(1, 10, new StationVisitDates
            {
                FirstStoppedDate = Day,
                FirstEntryExitDate = Earlier
            });

            var visit = await context.StationVisits.SingleAsync();
            Assert.Equal(Earlier, visit.FirstStoppedDate);
            Assert.Equal(Earlier, visit.FirstEntryExitDate);
        }

        [Fact]
        public async Task SetDates_KeepsTheDatesApartWhenTheyAreCoherent()
        {
            using var context = NewContext();
            var service = NewService(context);
            await service.MarkAsync(1, 10, StationVisitLevel.Stopped, Earlier, StationVisitSource.Web);

            // Passed through in 2019, actually got off in 2026: two real dates, both kept.
            await service.SetDatesAsync(1, 10, new StationVisitDates
            {
                FirstStoppedDate = Earlier,
                FirstEntryExitDate = Day
            });

            var visit = await context.StationVisits.SingleAsync();
            Assert.Equal(Earlier, visit.FirstStoppedDate);
            Assert.Equal(Day, visit.FirstEntryExitDate);
        }

        [Fact]
        public async Task SetDates_ClearingTheEntryExitDateLowersTheLevel()
        {
            using var context = NewContext();
            var service = NewService(context);
            await service.MarkAsync(1, 10, StationVisitLevel.EntryExit, Day, StationVisitSource.Web);

            await service.SetDatesAsync(1, 10, new StationVisitDates { FirstStoppedDate = Day });

            var visit = await context.StationVisits.SingleAsync();
            Assert.Equal(Day, visit.FirstStoppedDate);
            Assert.Null(visit.FirstEntryExitDate);
        }

        [Fact]
        public async Task SetDates_NeverCreatesAVisit()
        {
            using var context = NewContext();
            var service = NewService(context);

            // A date is a fact about a visit that exists; it may not conjure one.
            Assert.Null(await service.SetDatesAsync(1, 10, new StationVisitDates { FirstStoppedDate = Day }));
            Assert.Empty(context.StationVisits);
        }

        [Fact]
        public async Task SetDates_TakesTheDateFromTheTripRatherThanTheCaller()
        {
            using var context = NewContext();
            await SeedTripAsync(context, routeInstanceId: 7, ownerUserId: 1, date: Earlier);
            var service = NewService(context);
            await service.MarkAsync(1, 10, StationVisitLevel.Stopped, null, StationVisitSource.Web);

            await service.SetDatesAsync(1, 10, new StationVisitDates
            {
                FirstStoppedDate = Day,
                FirstStoppedRouteInstanceId = 7
            });

            // The trip is the more specific claim, so the pair cannot drift apart.
            var visit = await context.StationVisits.SingleAsync();
            Assert.Equal(Earlier, visit.FirstStoppedDate);
            Assert.Equal(7, visit.FirstStoppedRouteInstanceId);
        }

        [Fact]
        public async Task SetDates_RejectsATripTheUserDoesNotOwn()
        {
            using var context = NewContext();
            await SeedTripAsync(context, routeInstanceId: 7, ownerUserId: 99, date: Earlier);
            var service = NewService(context);
            await service.MarkAsync(1, 10, StationVisitLevel.Stopped, null, StationVisitSource.Web);

            await Assert.ThrowsAsync<ArgumentException>(() => service.SetDatesAsync(1, 10, new StationVisitDates
            {
                FirstStoppedRouteInstanceId = 7
            }));

            var visit = await context.StationVisits.SingleAsync();
            Assert.Null(visit.FirstStoppedDate);
        }

        [Fact]
        public async Task SetDates_AnsweringTheQuestionRetiresIt()
        {
            using var context = NewContext();
            var service = NewService(context);
            await service.MarkAsync(1, 10, StationVisitLevel.Stopped, null, StationVisitSource.Web);
            var visit = await context.StationVisits.SingleAsync();
            visit.DatingSkipped = true;
            await context.SaveChangesAsync();

            await service.SetDatesAsync(1, 10, new StationVisitDates { FirstStoppedDate = Day });

            visit = await context.StationVisits.SingleAsync();
            Assert.False(visit.DatingSkipped);
        }

        [Fact]
        public async Task MarkFromTrip_DatesAndLinksInOneGo()
        {
            using var context = NewContext();
            await SeedTripAsync(context, routeInstanceId: 7, ownerUserId: 1, date: Earlier);
            var service = NewService(context);

            await service.MarkFromTripAsync(1, 10, StationVisitLevel.EntryExit, 7, StationVisitSource.ImportSuggested);

            var visit = await context.StationVisits.SingleAsync();
            Assert.Equal(Earlier, visit.FirstStoppedDate);
            Assert.Equal(Earlier, visit.FirstEntryExitDate);
            Assert.Equal(7, visit.FirstStoppedRouteInstanceId);
            Assert.Equal(7, visit.FirstEntryExitRouteInstanceId);
            Assert.Equal(StationVisitSource.ImportSuggested, visit.Source);
        }

        [Fact]
        public async Task MarkFromTrip_StillOnlyEverAddsInformation()
        {
            using var context = NewContext();
            await SeedTripAsync(context, routeInstanceId: 7, ownerUserId: 1, date: Day);
            var service = NewService(context);
            await service.MarkAsync(1, 10, StationVisitLevel.Stopped, Earlier, StationVisitSource.Web);

            // A later trip cannot push an established earlier date forward, suggestion or not.
            await service.MarkFromTripAsync(1, 10, StationVisitLevel.Stopped, 7, StationVisitSource.ImportSuggested);

            var visit = await context.StationVisits.SingleAsync();
            Assert.Equal(Earlier, visit.FirstStoppedDate);
            Assert.Null(visit.FirstStoppedRouteInstanceId);
        }

        [Fact]
        public async Task MarkFromTrip_RejectsATripTheUserDoesNotOwn()
        {
            using var context = NewContext();
            await SeedTripAsync(context, routeInstanceId: 7, ownerUserId: 99, date: Day);
            var service = NewService(context);

            await Assert.ThrowsAsync<ArgumentException>(() =>
                service.MarkFromTripAsync(1, 10, StationVisitLevel.Stopped, 7, StationVisitSource.ImportSuggested));

            // And critically, no visit was conjured on the way to failing.
            Assert.Empty(context.StationVisits);
        }

        [Fact]
        public async Task SkipDating_RetiresTheVisitWithoutClaimingAnything()
        {
            using var context = NewContext();
            var service = NewService(context);
            await service.MarkAsync(1, 10, StationVisitLevel.Stopped, null, StationVisitSource.Web);

            Assert.True(await service.SkipDatingAsync(1, 10));

            // Still visited, still undated — just no longer asked about.
            var visit = await context.StationVisits.SingleAsync();
            Assert.True(visit.DatingSkipped);
            Assert.Null(visit.FirstStoppedDate);
        }

        [Fact]
        public async Task ResumeDating_PutsASetAsideVisitBackInTheQueue()
        {
            using var context = NewContext();
            var service = NewService(context);
            await service.MarkAsync(1, 10, StationVisitLevel.Stopped, null, StationVisitSource.Web);
            await service.SkipDatingAsync(1, 10);

            Assert.True(await service.ResumeDatingAsync(1, 10));

            // Undoing "can't remember" restores the question without asserting an answer to it.
            var visit = await context.StationVisits.SingleAsync();
            Assert.False(visit.DatingSkipped);
            Assert.Null(visit.FirstStoppedDate);
        }

        [Fact]
        public async Task ResumeDating_OnAnUnvisitedStationIsANoOp()
        {
            using var context = NewContext();
            var service = NewService(context);

            Assert.False(await service.ResumeDatingAsync(1, 10));
            Assert.Empty(context.StationVisits);
        }

        [Fact]
        public async Task SkipDating_OnAnUnvisitedStationIsANoOp()
        {
            using var context = NewContext();
            var service = NewService(context);

            Assert.False(await service.SkipDatingAsync(1, 10));
            Assert.Empty(context.StationVisits);
        }

        private static async Task SeedTripAsync(OVDBDatabaseContext context, int routeInstanceId, int ownerUserId, DateTime date)
        {
            context.Maps.Add(new Map { MapId = 1, UserId = ownerUserId, Name = "Trains", MapGuid = Guid.NewGuid() });
            context.Routes.Add(new Route { RouteId = 1, Name = "The line", Share = Guid.NewGuid() });
            context.RoutesMaps.Add(new RouteMap { RouteMapId = 1, RouteId = 1, MapId = 1 });
            context.RouteInstances.Add(new RouteInstance { RouteInstanceId = routeInstanceId, RouteId = 1, Date = date });
            await context.SaveChangesAsync();
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
