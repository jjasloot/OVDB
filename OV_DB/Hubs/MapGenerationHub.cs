using Microsoft.AspNetCore.SignalR;
using System;
using System.Threading.Tasks;

namespace OV_DB.Hubs;

public class MapGenerationHub : Hub
{
    public const string GenerationUpdateMethod = "GenerationUpdate";

    public async Task SendGenerationUpdateAsync(Guid id, int progress)
    {
        await Clients.All.SendAsync(GenerationUpdateMethod, id, progress);
    }
}
