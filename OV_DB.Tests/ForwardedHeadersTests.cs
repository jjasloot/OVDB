using System.Net;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.Extensions.Options;
using Xunit;

namespace OV_DB.Tests;

/// <summary>
/// The login rate limit is per client IP. Behind the Synology proxy the client IP comes from
/// X-Forwarded-For, which must only be believed when the proxy itself sent it.
/// </summary>
public class ForwardedHeadersTests
{
    private static async Task<IPAddress> ClientIpSeenAsync(string from, string forwardedFor, string[] knownProxies = null)
    {
        var options = new ForwardedHeadersOptions();
        Startup.ConfigureForwardedHeaders(options, knownProxies);
        IPAddress seen = null;
        var middleware = new ForwardedHeadersMiddleware(
            context => { seen = context.Connection.RemoteIpAddress; return Task.CompletedTask; },
            Microsoft.Extensions.Logging.Abstractions.NullLoggerFactory.Instance,
            Options.Create(options));

        var http = new DefaultHttpContext();
        http.Connection.RemoteIpAddress = IPAddress.Parse(from);
        http.Request.Headers["X-Forwarded-For"] = forwardedFor;
        await middleware.Invoke(http);
        return seen;
    }

    [Theory]
    [InlineData("172.17.0.1")]   // default Docker bridge gateway
    [InlineData("172.20.0.1")]   // a user-defined Docker network
    [InlineData("127.0.0.1")]    // host networking
    public async Task TakesTheClientIpFromTheProxy(string proxy)
    {
        Assert.Equal(IPAddress.Parse("203.0.113.7"), await ClientIpSeenAsync(proxy, "203.0.113.7"));
    }

    [Fact]
    public async Task TrustsAConfiguredLanProxy()
    {
        // The NAS proxying to its own LAN address, as seen with host networking.
        Assert.Equal(IPAddress.Parse("203.0.113.7"), await ClientIpSeenAsync("192.168.178.30", "203.0.113.7", ["192.168.178.30"]));
        Assert.Equal(IPAddress.Parse("192.168.178.30"), await ClientIpSeenAsync("192.168.178.30", "203.0.113.7"));
    }

    [Fact]
    public async Task IgnoresTheHeaderFromAnyoneElse()
    {
        Assert.Equal(IPAddress.Parse("198.51.100.9"), await ClientIpSeenAsync("198.51.100.9", "203.0.113.7"));
    }
}
