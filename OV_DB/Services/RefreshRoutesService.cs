using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Hosting;
using Microsoft.AspNetCore.SignalR;
using OV_DB.Hubs;
using OVDB_database.Database;
using OVDB_database.Models;

namespace OV_DB.Services
{
    public class RefreshRoutesService : IHostedService, IDisposable
    {
        private readonly OVDBDatabaseContext _dbContext;
        private readonly IRouteRegionsService _routeRegionsService;
        private readonly IHubContext<MapGenerationHub> _hubContext;
        private Timer _timer;

        public RefreshRoutesService(OVDBDatabaseContext dbContext, IRouteRegionsService routeRegionsService, IHubContext<MapGenerationHub> hubContext)
        {
            _dbContext = dbContext;
            _routeRegionsService = routeRegionsService;
            _hubContext = hubContext;
        }

        public Task StartAsync(CancellationToken cancellationToken)
        {
            _timer = new Timer(RefreshRoutes, null, TimeSpan.Zero, TimeSpan.FromHours(1));
            return Task.CompletedTask;
        }

        private async void RefreshRoutes(object state)
        {
            var regionId = (int)state;
            var adminClaim = (User.Claims.SingleOrDefault(c => c.Type == "admin").Value ?? "false");
            if (string.Equals(adminClaim, "false", StringComparison.OrdinalIgnoreCase))
            {
                return;
            }

            var routes = _dbContext.Routes.Where(r => r.RegionId == regionId).ToList();
            var totalRoutes = routes.Count;
            var processedRoutes = 0;

            foreach (var route in routes)
            {
                await _routeRegionsService.AssignRegionsToRouteAsync(route);
                processedRoutes++;
                var progress = (processedRoutes * 100) / totalRoutes;
                await _hubContext.Clients.All.SendAsync("GenerationUpdate", regionId, progress);
            }

            await _dbContext.SaveChangesAsync();

            await _hubContext.Clients.All.SendAsync("GenerationUpdate", regionId, 100);
        }

        public Task StopAsync(CancellationToken cancellationToken)
        {
            _timer?.Change(Timeout.Infinite, 0);
            return Task.CompletedTask;
        }

        public void Dispose()
        {
            _timer?.Dispose();
        }
    }
}
