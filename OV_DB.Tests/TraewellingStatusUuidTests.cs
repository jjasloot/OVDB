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
using Newtonsoft.Json;
using OV_DB.Hubs;
using OV_DB.Models;
using OV_DB.Services;
using OVDB_database.Database;
using OVDB_database.Models;

namespace OV_DB.Tests;

/// <summary>
/// Träwelling's status ids are moving to UUIDs. OVDB stores both, fills the UUID on a resync, and keeps
/// working when a status arrives with only a UUID: known ones are matched by it, new ones get a local key.
/// </summary>
public class TraewellingStatusUuidTests
{
    private const string KnownUuid = "1671deca-ce69-448d-967e-83911020a66a";
    private const string NewUuid = "9b2f4c1e-0000-4000-8000-000000000001";

    private sealed class OnePageHandler(string page) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) =>
            Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(page) });
    }

    private static (TrawellingService Service, User User, OVDBDatabaseContext Context) NewService(string statusesJson)
    {
        var page = "{\"data\":[" + statusesJson + "],\"links\":{\"next\":null}}";
        var factory = new Mock<IHttpClientFactory>();
        factory.Setup(f => f.CreateClient(It.IsAny<string>()))
            .Returns(() => new HttpClient(new OnePageHandler(page)) { BaseAddress = new Uri("https://traewelling.test/") });
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string> { ["Traewelling:BaseUrl"] = "https://traewelling.test/api/v1" })
            .Build();
        var context = new OVDBDatabaseContext(new DbContextOptionsBuilder<OVDBDatabaseContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);
        var api = new TraewellingApiClient(factory.Object, configuration, context, NullLogger<TraewellingApiClient>.Instance, Mock.Of<ITraewellingRateLimiter>());
        var service = new TrawellingService(api,
            new TraewellingTripMapper(new Mock<ITimezoneService>().Object, NullLogger<TraewellingTripMapper>.Instance),
            new Mock<ITimezoneService>().Object, context, NullLogger<TrawellingService>.Instance,
            new MemoryCache(new MemoryCacheOptions()), new Mock<IHubContext<TraewellingHub>>().Object);
        var user = new User
        {
            Id = 1, TrawellingUsername = "gertrud", TrawellingAccessToken = "token", TrawellingRefreshToken = "refresh",
            TrawellingTokenExpiresAt = DateTime.UtcNow.AddHours(1),
            Email = "gertrud@example.test",
            Password = "x"
        };
        context.Users.Add(user);

        // One imported trip (status 101) on one of the user's maps.
        context.Maps.Add(new Map { MapId = 1, UserId = 1, Name = "Map", MapGuid = Guid.NewGuid() });
        context.Routes.Add(new Route { RouteId = 1, Name = "Route", Share = Guid.NewGuid() });
        context.RoutesMaps.Add(new RouteMap { RouteMapId = 1, RouteId = 1, MapId = 1 });
        context.RouteInstances.Add(new RouteInstance { RouteInstanceId = 1, RouteId = 1, Date = new DateTime(2017, 10, 15), TrawellingStatusId = 101 });
        context.SaveChanges();
        return (service, user, context);
    }

    private static string Status(string id, string uuid = null) =>
        "{\"id\":" + id + (uuid == null ? "" : ",\"uuid\":\"" + uuid + "\"")
        + ",\"checkin\":{\"manualDeparture\":\"2017-10-15T10:00:00+02:00\"}}";

    [Fact]
    public async Task FullResyncGivesExistingImportsTheirUuid()
    {
        var (service, user, context) = NewService(Status("101", KnownUuid));

        await service.SweepInboxAsync(user, TrawellingSweepMode.Full);

        Assert.Equal(KnownUuid, (await context.RouteInstances.SingleAsync()).TrawellingStatusUuid);
        Assert.Empty(context.TrawellingInboxStatuses); // already imported, not offered again
    }

    [Fact]
    public async Task WithoutNumericIdsAKnownStatusIsMatchedByItsUuid()
    {
        var (service, user, context) = NewService(Status($"\"{KnownUuid}\""));
        var trip = await context.RouteInstances.SingleAsync();
        trip.TrawellingStatusUuid = KnownUuid;
        await context.SaveChangesAsync();

        await service.SweepInboxAsync(user, TrawellingSweepMode.Full);

        // Recognised as the trip already imported, so nothing new lands in the inbox.
        Assert.Empty(context.TrawellingInboxStatuses);
    }

    [Fact]
    public async Task WithoutNumericIdsANewStatusGetsALocalKeyAndKeepsItsUuid()
    {
        var (service, user, context) = NewService(Status($"\"{NewUuid}\""));

        await service.SweepInboxAsync(user, TrawellingSweepMode.Full);

        var row = await context.TrawellingInboxStatuses.SingleAsync();
        Assert.True(row.TrawellingStatusId < 0);
        Assert.Equal(NewUuid, row.TrawellingStatusUuid);
    }

    [Fact]
    public async Task ABackfillIsNeededOnlyWhileImportedTripsLackTheirUuid()
    {
        var (_, _, context) = NewService(Status("101", KnownUuid));

        Assert.True(await TraewellingInboxSweepService.NeedsUuidBackfillAsync(context, 1, default));
        Assert.False(await TraewellingInboxSweepService.NeedsUuidBackfillAsync(context, 2, default)); // not their trip

        (await context.RouteInstances.SingleAsync()).TrawellingStatusUuid = KnownUuid;
        await context.SaveChangesAsync();
        Assert.False(await TraewellingInboxSweepService.NeedsUuidBackfillAsync(context, 1, default));
    }

    [Fact]
    public async Task OnlyANewStatusFromTraewellingCountsAsReceived()
    {
        var (service, user, context) = NewService(Status("101", KnownUuid));
        await service.SweepInboxAsync(user, TrawellingSweepMode.Full);
        Assert.Null((await context.Users.SingleAsync()).TraewellingLastReceivedAt); // only a known trip came by

        var (service2, user2, context2) = NewService(Status("202", NewUuid));
        await service2.SweepInboxAsync(user2, TrawellingSweepMode.Full);
        Assert.NotNull((await context2.Users.SingleAsync()).TraewellingLastReceivedAt);
    }

    [Theory]
    [InlineData("123", 123, null)]
    [InlineData("\"123\"", 123, null)]
    [InlineData($"\"{KnownUuid}\"", 0, KnownUuid)]
    public void StatusIdParsesAsNumberOrUuid(string idJson, int expectedId, string expectedUuid)
    {
        var status = JsonConvert.DeserializeObject<TrawellingStatus>($$"""{"id":{{idJson}}}""");

        Assert.Equal(expectedId, status.Id);
        Assert.Equal(expectedUuid, status.Uuid);
    }
}
