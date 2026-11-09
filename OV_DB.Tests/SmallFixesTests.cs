using System.Security.Claims;
using OV_DB.Controllers;
using OV_DB.Extensions;
using OVDB_database.Models;
using Xunit;

namespace OV_DB.Tests;

public class SmallFixesTests
{
    [Theory]
    [InlineData(1.3333333, "1:20")]
    [InlineData(0.5, "0:30")]
    [InlineData(25.999, "26:00")]
    [InlineData(0, "0:00")]
    public void TripReportDurationsAreHoursAndMinutes(double hours, string expected)
    {
        Assert.Equal(expected, TripReportController.FormatHours(hours));
    }

    private static ClaimsPrincipal Caller(int? userId = null, bool admin = false)
    {
        if (userId == null) return new ClaimsPrincipal(new ClaimsIdentity());
        return new ClaimsPrincipal(new ClaimsIdentity([
            new Claim(ClaimTypes.NameIdentifier, userId.ToString()),
            new Claim("admin", admin ? "true" : "false")], "test"));
    }

    [Theory]
    [InlineData("my-map", null, false, true)]   // shared: anyone
    [InlineData(null, null, false, false)]      // private, anonymous
    [InlineData(null, 2, false, false)]         // private, another user
    [InlineData(null, 1, false, true)]          // private, owner
    [InlineData(null, 2, true, true)]           // private, admin
    public void AMapGuidAloneDoesNotOpenAPrivateMap(string sharingLinkName, int? callerId, bool admin, bool expected)
    {
        var map = new Map { UserId = 1, SharingLinkName = sharingLinkName };
        Assert.Equal(expected, Caller(callerId, admin).CanView(map));
    }
}
