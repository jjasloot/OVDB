using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using OV_DB.Controllers;
using OV_DB.Services;
using OVDB_database.Database;
using Xunit;

namespace OV_DB.Tests;

/// <summary>
/// A connect link opened in another browser must not attach that browser's Träwelling account to
/// the OVDB user who generated the link: the callback needs the state cookie set by connect.
/// </summary>
public class TraewellingOAuthBrowserBindingTests
{
    private static (TraewellingController Controller, Mock<ITraewellingApiClient> Api) NewController(string cookieState)
    {
        var api = new Mock<ITraewellingApiClient>();
        int? userId = 7;
        api.Setup(a => a.ValidateAndConsumeState("the-state", out userId)).Returns(true);
        api.Setup(a => a.ExchangeCodeForTokensAsync("code", "the-state", 7)).ReturnsAsync(true);

        var http = new DefaultHttpContext();
        if (cookieState != null)
        {
            http.Request.Headers.Cookie = $"{TraewellingController.OAuthStateCookie}={cookieState}";
        }
        var controller = new TraewellingController(
            Mock.Of<ITrawellingService>(),
            api.Object,
            new OVDBDatabaseContext(new DbContextOptionsBuilder<OVDBDatabaseContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options),
            NullLogger<TraewellingController>.Instance,
            Mock.Of<ITraewellingRateLimiter>(),
            Mock.Of<ITraewellingResyncQueue>())
        {
            ControllerContext = new ControllerContext { HttpContext = http }
        };
        return (controller, api);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("someone-elses-state")]
    public async Task ACallbackFromAnotherBrowserIsRefused(string cookieState)
    {
        var (controller, api) = NewController(cookieState);

        var result = Assert.IsType<ContentResult>(await controller.HandleCallback("code", "the-state"));

        Assert.Contains("oauth-error", result.Content);
        int? ignored;
        api.Verify(a => a.ValidateAndConsumeState(It.IsAny<string>(), out ignored), Times.Never);
        api.Verify(a => a.ExchangeCodeForTokensAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<int>()), Times.Never);
    }

    [Fact]
    public async Task TheBrowserThatStartedTheFlowConnects()
    {
        var (controller, api) = NewController("the-state");

        var result = Assert.IsType<ContentResult>(await controller.HandleCallback("code", "the-state"));

        Assert.Contains("oauth-success", result.Content);
        api.Verify(a => a.ExchangeCodeForTokensAsync("code", "the-state", 7), Times.Once);
    }
}
