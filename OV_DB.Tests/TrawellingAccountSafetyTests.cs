using System;
using System.Collections.Generic;
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
using Xunit;

namespace OV_DB.Tests;

/// <summary>
/// Two ways the Träwelling integration could damage data it does not own: linking a check-in onto
/// another user's trip, and wiping every user's tokens when the failure was not theirs.
/// </summary>
public class TrawellingAccountSafetyTests
{
    private sealed class CannedHandler(HttpStatusCode status, string body) : HttpMessageHandler
    {
        public int Calls { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Calls++;
            return Task.FromResult(new HttpResponseMessage(status) { Content = new StringContent(body) });
        }
    }

    private static (TrawellingService Service, OVDBDatabaseContext Context, CannedHandler Handler) NewService(
        HttpStatusCode status = HttpStatusCode.OK, string body = "{}")
    {
        var handler = new CannedHandler(status, body);
        var factory = new Mock<IHttpClientFactory>();
        factory.Setup(f => f.CreateClient(It.IsAny<string>()))
            .Returns(() => new HttpClient(handler));

        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string>
            {
                ["Traewelling:BaseUrl"] = "https://traewelling.test/api/v1",
                ["Traewelling:TokenUrl"] = "https://traewelling.test/oauth/token",
                ["Traewelling:ClientId"] = "id",
                ["Traewelling:ClientSecret"] = "secret"
            })
            .Build();

        var context = new OVDBDatabaseContext(new DbContextOptionsBuilder<OVDBDatabaseContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options);

        var service = new TrawellingService(
            factory.Object,
            configuration,
            new Mock<ITimezoneService>().Object,
            context,
            NullLogger<TrawellingService>.Instance,
            new MemoryCache(new MemoryCacheOptions()),
            Mock.Of<ITraewellingRateLimiter>(),
            new Mock<IHubContext<TraewellingHub>>().Object);

        return (service, context, handler);
    }

    private static User NewUser(int id) => new()
    {
        Id = id,
        Email = $"user{id}@test",
        Password = "x",
        TrawellingAccessToken = "token",
        TrawellingRefreshToken = "refresh",
        TrawellingTokenExpiresAt = DateTime.UtcNow.AddHours(1)
    };

    [Fact]
    public async Task CannotLinkACheckInToAnotherUsersTrip()
    {
        var (service, context, handler) = NewService();
        var attacker = NewUser(1);
        var victim = NewUser(2);
        context.Users.AddRange(attacker, victim);
        context.Maps.Add(new Map { MapId = 20, UserId = victim.Id, Name = "Victim's map", MapGuid = Guid.NewGuid() });
        context.Routes.Add(new Route { RouteId = 1, Name = "Victim's line", Share = Guid.NewGuid() });
        context.RoutesMaps.Add(new RouteMap { RouteMapId = 1, RouteId = 1, MapId = 20 });
        context.RouteInstances.Add(new RouteInstance { RouteInstanceId = 100, RouteId = 1, Date = new DateTime(2026, 8, 1) });
        await context.SaveChangesAsync();

        var result = await service.LinkStatusToRouteInstanceAsync(attacker, statusId: 555, routeInstanceId: 100);

        Assert.Null(result);
        Assert.Null((await context.RouteInstances.SingleAsync()).TrawellingStatusId);
        Assert.Equal(0, handler.Calls);
    }

    [Theory]
    [InlineData(HttpStatusCode.Unauthorized, "{\"error\":\"invalid_client\"}")]
    [InlineData(HttpStatusCode.BadRequest, "{\"error\":\"invalid_request\"}")]
    [InlineData(HttpStatusCode.BadRequest, "<html>proxy error</html>")]
    [InlineData(HttpStatusCode.InternalServerError, "")]
    public async Task KeepsTokensWhenTheRefreshTokenIsNotTheProblem(HttpStatusCode status, string body)
    {
        var (service, context, handler) = NewService(status, body);
        var user = NewUser(1);
        context.Users.Add(user);
        await context.SaveChangesAsync();

        Assert.False(await service.RefreshTokensAsync(user));

        Assert.True(handler.Calls > 0);
        Assert.Equal("refresh", user.TrawellingRefreshToken);
    }

    [Fact]
    public async Task ClearsTokensWhenTheRefreshTokenIsRejected()
    {
        var (service, context, _) = NewService(HttpStatusCode.BadRequest,
            "{\"error\":\"invalid_grant\",\"error_description\":\"The refresh token is invalid.\"}");
        var user = NewUser(1);
        context.Users.Add(user);
        await context.SaveChangesAsync();

        Assert.False(await service.RefreshTokensAsync(user));

        Assert.Null(user.TrawellingRefreshToken);
        Assert.Null(user.TrawellingAccessToken);
    }
}
