using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;

namespace OV_DB.Hubs;

[Authorize]
public class MapGenerationHub : Hub
{
    public const string GenerationUpdateMethod = "GenerationUpdate";
    public const string RegionUpdateMethod = "RefreshRoutes";
    public const string RegionStationUpdateMethod = "RefreshStations";

    // Clients join a group keyed by their own map-generation request id so progress for that
    // request is delivered only to them, instead of being broadcast to every connected client.
    public Task JoinGenerationGroup(string requestIdentifier)
        => Groups.AddToGroupAsync(Context.ConnectionId, requestIdentifier);
}
