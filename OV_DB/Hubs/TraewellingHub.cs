using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;

namespace OV_DB.Hubs;

/// <summary>
/// Per-user live updates for the Träwelling unimported-trips list, fed by webhook events.
/// Authenticated with the regular JWT (access_token query parameter on the handshake);
/// events go through Clients.User so they only reach the owning user's connections.
/// </summary>
[Authorize]
public class TraewellingHub : Hub
{
    // Carries the trip DTO pre-serialized with the REST API's Newtonsoft camelCase settings
    // (SignalR's own System.Text.Json serializer would render enums and casing differently),
    // so the client can JSON.parse it into the exact same shape the list endpoint returns.
    public const string PendingTripUpsertedMethod = "PendingTripUpserted";
    public const string PendingTripRemovedMethod = "PendingTripRemoved";
    public const string ConflictUpsertedMethod = "ConflictUpserted";
}
