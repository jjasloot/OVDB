using System;
using System.Linq;
using System.Security.Claims;

namespace OV_DB.Extensions;

public static class ClaimsPrincipalExtensions
{
    /// <summary>
    /// Returns the authenticated user's id, or -1 when there is no (valid) id claim.
    /// Never throws on a missing claim, unlike dereferencing SingleOrDefault(...).Value.
    /// </summary>
    public static int GetUserId(this ClaimsPrincipal user)
    {
        var claim = user?.Claims.FirstOrDefault(c => c.Type == ClaimTypes.NameIdentifier);
        return int.TryParse(claim?.Value, out var id) ? id : -1;
    }

    /// <summary>
    /// True when the principal carries the "admin" claim set to "true".
    /// </summary>
    public static bool IsAdmin(this ClaimsPrincipal user)
    {
        var claim = user?.Claims.FirstOrDefault(c => c.Type == "admin");
        return string.Equals(claim?.Value, "true", StringComparison.OrdinalIgnoreCase);
    }
}
