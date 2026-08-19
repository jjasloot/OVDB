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
/// The calling pattern behind every Träwelling import suggestion comes out of one HTTP response, and
/// its shape is the one thing about suggestions that no other test touches: a wrong reading here
/// silently produces no suggestions rather than an error.
/// </summary>
public class TrawellingStopoverFetchTests
{
    // The /stopovers endpoint takes a comma-separated list of trip ids, so its data object is keyed
    // by trip id rather than being a bare array.
    private const string StopoversJson = """
    {
      "data": {
        "987": [
          {
            "uuid": "aaaaaaaa-0000-0000-0000-000000000000",
            "station": { "id": 4711, "name": "Karlsruhe Hbf", "latitude": 48.993207, "longitude": 8.400977 },
            "departurePlanned": "2026-08-01T10:05:00+02:00"
          },
          {
            "uuid": "bbbbbbbb-0000-0000-0000-000000000000",
            "station": { "id": 4713, "name": "Pforzheim Hbf", "latitude": 48.893, "longitude": 8.708 },
            "arrivalPlanned": "2026-08-01T10:28:00+02:00"
          },
          {
            "uuid": "cccccccc-0000-0000-0000-000000000000",
            "station": { "id": 4712, "name": "Stuttgart Hbf", "latitude": 48.784084, "longitude": 9.181635 },
            "arrivalPlanned": "2026-08-01T10:50:00+02:00"
          }
        ]
      }
    }
    """;

    private sealed class CannedHandler(HttpStatusCode status, string body) : HttpMessageHandler
    {
        public string RequestedPath { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            RequestedPath = request.RequestUri?.PathAndQuery;
            return Task.FromResult(new HttpResponseMessage(status) { Content = new StringContent(body) });
        }
    }

    private static (TrawellingService Service, User User, CannedHandler Handler) NewService(
        HttpStatusCode status, string body)
    {
        var handler = new CannedHandler(status, body);
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

        var hub = new Mock<IHubContext<TraewellingHub>>();
        var service = new TrawellingService(
            factory.Object,
            configuration,
            new Mock<ITimezoneService>().Object,
            context,
            NullLogger<TrawellingService>.Instance,
            new MemoryCache(new MemoryCacheOptions()),
            Mock.Of<ITraewellingRateLimiter>(),
            hub.Object);

        var user = new User
        {
            Id = 1,
            TrawellingAccessToken = "token",
            TrawellingRefreshToken = "refresh",
            // Comfortably outside the five-minute margin, so nothing tries to refresh.
            TrawellingTokenExpiresAt = DateTime.UtcNow.AddHours(1)
        };

        return (service, user, handler);
    }

    [Fact]
    public async Task ReadsEveryStationTheTripCallsAt()
    {
        var (service, user, handler) = NewService(HttpStatusCode.OK, StopoversJson);

        var stopovers = await service.GetTripStopoversAsync(user, 987);

        Assert.Equal(
            ["Karlsruhe Hbf", "Pforzheim Hbf", "Stuttgart Hbf"],
            stopovers.Select(s => s.Station.Name));
        // Coordinates are what the matcher works from; a stopover without them is useless.
        Assert.All(stopovers, s =>
        {
            Assert.NotNull(s.Station.Latitude);
            Assert.NotNull(s.Station.Longitude);
        });
        Assert.Equal("/api/v1/stopovers/987", handler.RequestedPath);
    }

    [Fact]
    public async Task ReadsThemEvenWhenTheResponseIsKeyedBySomethingElse()
    {
        // We only ever ask for one trip, so the single key is the one we want whatever it is called.
        var body = StopoversJson.Replace("\"987\"", "\"someOtherKey\"");
        var (service, user, _) = NewService(HttpStatusCode.OK, body);

        var stopovers = await service.GetTripStopoversAsync(user, 987);

        Assert.Equal(3, stopovers.Count);
    }

    [Theory]
    [InlineData(HttpStatusCode.NotFound, "{}")]
    // Not 429: that one is retried with backoff, which belongs to SendAsync rather than here.
    [InlineData(HttpStatusCode.Forbidden, "{}")]
    [InlineData(HttpStatusCode.OK, "not json at all")]
    [InlineData(HttpStatusCode.OK, "{\"data\": []}")]
    public async Task AMissingCallingPatternIsEmpty(HttpStatusCode status, string body)
    {
        // Suggestions are a bonus on top of the import. Losing them must never throw into it.
        var (service, user, _) = NewService(status, body);

        Assert.Empty(await service.GetTripStopoversAsync(user, 987));
    }

    [Fact]
    public async Task WithoutAUsableTokenNothingIsRequested()
    {
        var (service, user, handler) = NewService(HttpStatusCode.OK, StopoversJson);
        user.TrawellingAccessToken = null;
        user.TrawellingRefreshToken = null;

        Assert.Empty(await service.GetTripStopoversAsync(user, 987));
        Assert.Null(handler.RequestedPath);
    }
}
