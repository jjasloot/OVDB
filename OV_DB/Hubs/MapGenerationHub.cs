using Microsoft.AspNetCore.SignalR;

namespace OV_DB.Hubs;

public class MapGenerationHub : Hub
{
    public const string GenerationUpdateMethod = "GenerationUpdate";
    public const string RegionUpdateMethod = "RefreshRoutes";
    public const string RegionStationUpdateMethod = "RefreshStations";
}
