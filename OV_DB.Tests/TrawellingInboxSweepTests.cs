using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using OV_DB.Hubs;
using OV_DB.Services;
using OVDB_database.Database;
using OVDB_database.Models;

namespace OV_DB.Tests;

/// <summary>
/// The statuses listing is ordered newest-first, so a check-in that was missed while newer ones
/// were already imported sits behind a page holding nothing new. Which sweep mode walks past
/// such a page decides whether it can ever be found again.
/// </summary>
public class TrawellingInboxSweepTests
{
    // Only the fields the sweep reads: the id it dedupes on and the departure it sorts and
    // bounds the delete-healing by.
    private static string Page(string next, params (int Id, string Departure)[] statuses)
    {
        var data = string.Join(",", statuses.Select(s =>
            $$$"""{"id":{{{s.Id}}},"checkin":{"manualDeparture":"{{{s.Departure}}}"}}"""));
        var nextLink = next == null ? "null" : $"\"{next}\"";
        return $$$"""{"data":[{{{data}}}],"links":{"next":{{{nextLink}}}}}""";
    }

    // Progress<T> hands its reports to the thread pool, which would make the order they are
    // asserted in a race.
    private sealed class InlineProgress<T>(Action<T> report) : IProgress<T>
    {
        public void Report(T value) => report(value);
    }

    private sealed class PagedHandler(Dictionary<int, string> pages) : HttpMessageHandler
    {
        public List<int> RequestedPages { get; } = [];

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var page = int.Parse(request.RequestUri!.Query.Split("page=")[1]);
            RequestedPages.Add(page);
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(pages[page])
            });
        }
    }

    private static (TrawellingService Service, User User, OVDBDatabaseContext Context, PagedHandler Handler) NewService(
        Dictionary<int, string> pages)
    {
        var handler = new PagedHandler(pages);
        var factory = new Mock<IHttpClientFactory>();
        factory.Setup(f => f.CreateClient(It.IsAny<string>()))
            .Returns(() => new HttpClient(handler) { BaseAddress = new Uri("https://traewelling.test/") });

        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string>
            {
                ["Traewelling:BaseUrl"] = "https://traewelling.test/api/v1"
            })
            .Build();

        var options = new DbContextOptionsBuilder<OVDBDatabaseContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        var context = new OVDBDatabaseContext(options);

        var service = new TrawellingService(
            factory.Object,
            configuration,
            new Mock<ITimezoneService>().Object,
            context,
            NullLogger<TrawellingService>.Instance,
            new MemoryCache(new MemoryCacheOptions()),
            Mock.Of<ITraewellingRateLimiter>(),
            new Mock<IHubContext<TraewellingHub>>().Object);

        var user = new User
        {
            Id = 1,
            // Set, so the sweep doesn't first call /auth/user to look it up.
            TrawellingUsername = "gertrud",
            TrawellingAccessToken = "token",
            TrawellingRefreshToken = "refresh",
            TrawellingTokenExpiresAt = DateTime.UtcNow.AddHours(1)
        };

        return (service, user, context, handler);
    }

    // Page 1 holds two check-ins already in the inbox, page 2 an older one that never arrived.
    private static Dictionary<int, string> NewestKnownOldestMissing() => new()
    {
        [1] = Page("https://traewelling.test/api/v1/user/gertrud/statuses?page=2",
            (101, "2026-08-20T10:00:00+02:00"),
            (102, "2026-08-19T10:00:00+02:00")),
        [2] = Page(null, (50, "2026-06-01T10:00:00+02:00")),
    };

    private static void SeedInbox(OVDBDatabaseContext context, params (int StatusId, string Departure)[] statuses)
    {
        foreach (var (statusId, departure) in statuses)
        {
            context.TrawellingInboxStatuses.Add(new TrawellingInboxStatus
            {
                UserId = 1,
                TrawellingStatusId = statusId,
                PayloadJson = "{}",
                State = TrawellingInboxState.Pending,
                Source = TrawellingInboxSource.Sweep,
                DepartureAt = DateTimeOffset.Parse(departure).UtcDateTime,
                ReceivedAt = DateTime.UtcNow,
                LastEventAt = DateTime.UtcNow,
            });
        }
        context.SaveChanges();
    }

    [Fact]
    public async Task ForcedSweepStopsAtTheFirstPageWithoutAnythingNew()
    {
        var (service, user, context, handler) = NewService(NewestKnownOldestMissing());
        SeedInbox(context, (101, "2026-08-20T10:00:00+02:00"), (102, "2026-08-19T10:00:00+02:00"));

        var result = await service.SweepInboxAsync(user, TrawellingSweepMode.Force);

        Assert.True(result.Success);
        Assert.Equal(0, result.Added);
        Assert.Equal([1], handler.RequestedPages);
        Assert.DoesNotContain(50, context.TrawellingInboxStatuses.Select(s => s.TrawellingStatusId));
    }

    [Fact]
    public async Task FullSweepWalksPastKnownPagesAndPicksUpTheMissedCheckIn()
    {
        var (service, user, context, handler) = NewService(NewestKnownOldestMissing());
        SeedInbox(context, (101, "2026-08-20T10:00:00+02:00"), (102, "2026-08-19T10:00:00+02:00"));
        var reported = new List<TrawellingSweepProgress>();

        var result = await service.SweepInboxAsync(user, TrawellingSweepMode.Full,
            new InlineProgress<TrawellingSweepProgress>(reported.Add));

        Assert.True(result.Success);
        Assert.True(result.ReachedEnd);
        Assert.Equal(1, result.Added);
        Assert.Equal([1, 2], handler.RequestedPages);
        Assert.Contains(50, context.TrawellingInboxStatuses.Select(s => s.TrawellingStatusId));
        // One report per page, so the page it is on is visible while the walk runs.
        Assert.Equal([(1, 0), (2, 1)], reported.Select(p => (p.PagesRead, p.Added)));
    }

    [Fact]
    public async Task FullSweepKeepsStatusesAlreadyKnownFromTheSweptRange()
    {
        var (service, user, context, _) = NewService(NewestKnownOldestMissing());
        SeedInbox(context, (101, "2026-08-20T10:00:00+02:00"), (102, "2026-08-19T10:00:00+02:00"));

        await service.SweepInboxAsync(user, TrawellingSweepMode.Full);

        Assert.Equal([50, 101, 102], context.TrawellingInboxStatuses
            .Select(s => s.TrawellingStatusId).OrderBy(id => id));
    }

    [Fact]
    public async Task StaleCheckSkipsTheApiEntirelyWhenTheLastSweepIsRecent()
    {
        var (service, user, _, handler) = NewService(NewestKnownOldestMissing());
        user.TrawellingLastSweepAt = DateTime.UtcNow.AddMinutes(-5);

        var result = await service.SweepInboxAsync(user);

        Assert.True(result.Success);
        Assert.Empty(handler.RequestedPages);
    }
}
