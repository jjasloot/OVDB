using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;

namespace OV_DB.Hubs;

/// <summary>
/// Per-user live updates for the station screens. Authenticated with the regular JWT
/// (access_token query parameter on the handshake); events go through Clients.User so they
/// only reach the user who asked for the work.
/// </summary>
[Authorize]
public class StationsHub : Hub
{
    // Building the route index takes seconds and outlives the request that asked for it, so
    // the backfill page hears how it is getting on only here.
    public const string WarmupProgressMethod = "BackfillWarmupProgress";
    public const string WarmupFinishedMethod = "BackfillWarmupFinished";
}
