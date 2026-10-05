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
    /// <summary>(regionId, progress 0..100) while a region's rail networks are being assembled.</summary>
    public const string NetworkUpdateMethod = "RefreshNetwork";
    /// <summary>(regionNetworkId, progress 0..100) while a network's routes are being rematched.</summary>
    public const string NetworkMatchMethod = "RefreshNetworkMatches";

    // Clients join a group keyed by their own map-generation request id so progress for that
    // request is delivered only to them, instead of being broadcast to every connected client.
    public Task JoinGenerationGroup(string requestIdentifier)
        => Groups.AddToGroupAsync(Context.ConnectionId, requestIdentifier);
}
