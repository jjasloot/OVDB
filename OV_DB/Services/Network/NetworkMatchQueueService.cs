using System;
using System.Collections.Concurrent;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using OV_DB.Hubs;

namespace OV_DB.Services.Network;

/// <summary>
/// Runs <see cref="NetworkMatchWorkItem"/>s in the background, one at a time: route saves enqueue
/// <see cref="RouteChanged"/>, assembly enqueues <see cref="NetworkChanged"/>. Same shape as the
/// other queue services; a failed item never stops the loop.
/// </summary>
public class NetworkMatchQueueService(IServiceProvider serviceProvider, IHubContext<MapGenerationHub> hubContext, ILogger<NetworkMatchQueueService> logger) : IHostedService, IDisposable
{
    public static readonly ConcurrentQueue<NetworkMatchWorkItem> Queue = new();

    private Task _backgroundTask;
    private CancellationTokenSource _cancellationTokenSource;

    public Task StartAsync(CancellationToken cancellationToken)
    {
        _cancellationTokenSource = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        _backgroundTask = Task.Run(() => ProcessQueueAsync(_cancellationTokenSource.Token));
        return Task.CompletedTask;
    }

    private async Task ProcessQueueAsync(CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            if (Queue.TryDequeue(out var item))
            {
                try
                {
                    using var scope = serviceProvider.CreateScope();
                    var service = scope.ServiceProvider.GetRequiredService<INetworkMatchService>();
                    switch (item)
                    {
                        case RouteChanged route:
                            await service.MatchRouteAsync(route.RouteId, cancellationToken);
                            break;
                        case NetworkChanged network:
                            await service.RematchNetworkAsync(network.RegionNetworkId,
                                p => hubContext.Clients.All.SendAsync(MapGenerationHub.NetworkMatchMethod, network.RegionNetworkId, p, cancellationToken),
                                cancellationToken);
                            break;
                    }
                }
                catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
                {
                    break;
                }
                catch (Exception ex)
                {
                    logger.LogError(ex, "Network match work item {Item} failed; continuing with the queue", item);
                }
            }
            else
            {
                try
                {
                    await Task.Delay(1000, cancellationToken);
                }
                catch (OperationCanceledException)
                {
                    break;
                }
            }
        }
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        _cancellationTokenSource?.Cancel();
        return _backgroundTask == null ? Task.CompletedTask : Task.WhenAny(_backgroundTask, Task.Delay(Timeout.Infinite, cancellationToken));
    }

    public void Dispose()
    {
        _cancellationTokenSource?.Cancel();
    }
}
