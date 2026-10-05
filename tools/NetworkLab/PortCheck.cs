using System.Diagnostics;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using OV_DB.Hubs;
using OV_DB.Services;
using OV_DB.Services.Network;
using OVDB_database.Database;
using OVDB_database.Models;

namespace NetworkLab;

/// <summary>
/// Acceptance check for the port: runs the app's real <see cref="RegionNetworkAssemblyService"/>
/// against the dev database, fed from the lab's cached Overpass responses (no Overpass call), and
/// prints the persisted network rows next to the lab's numbers. Applies pending migrations to the
/// dev database first — never point this at production.
/// </summary>
public static class PortCheck
{
    public static async Task RunAsync(int regionId, string connectionString, string dataDir)
    {
        if (!connectionString.Contains("127.0.0.1") && !connectionString.Contains("localhost"))
            throw new InvalidOperationException("port-check only runs against a local dev database");

        // The app's cache layout is <root>/<regionId>/{track,stops,routes}.json; seed it from the lab's.
        var cacheRoot = Path.Combine(dataDir, "app-cache");
        var regionCache = Path.Combine(cacheRoot, regionId.ToString());
        Directory.CreateDirectory(regionCache);
        File.Copy(OverpassClient.CachePath(dataDir, regionId), Path.Combine(regionCache, "track.json"), overwrite: true);
        File.Copy(OverpassClient.StopsCachePath(dataDir, regionId), Path.Combine(regionCache, "stops.json"), overwrite: true);
        File.Copy(OverpassClient.RoutesCachePath(dataDir, regionId), Path.Combine(regionCache, "routes.json"), overwrite: true);

        var services = new ServiceCollection();
        services.AddLogging(b => b.AddSimpleConsole(o => o.SingleLine = true)
            .SetMinimumLevel(LogLevel.Information)
            .AddFilter("Microsoft.EntityFrameworkCore", LogLevel.Warning));
        services.AddDbContext<OVDBDatabaseContext>(o => o.UseMySql(connectionString, ServerVersion.AutoDetect(connectionString), m => m.UseNetTopologySuite()));
        services.AddSingleton<IConfiguration>(new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string> { ["Networks:CachePath"] = cacheRoot })
            .Build());
        services.AddSingleton<IHubContext<MapGenerationHub>, ConsoleHubContext>();
        services.AddSingleton<IOverpassService, NoOverpass>();
        services.AddSingleton<RegionNetworkAssemblyService>();
        await using var provider = services.BuildServiceProvider();

        using (var scope = provider.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<OVDBDatabaseContext>();
            var pending = (await db.Database.GetPendingMigrationsAsync()).ToList();
            Console.WriteLine(pending.Count == 0 ? "Dev db schema is current." : $"Applying {pending.Count} migration(s) to the dev db: {string.Join(", ", pending)}");
            await db.Database.MigrateAsync();
        }

        var watch = Stopwatch.StartNew();
        var service = provider.GetRequiredService<RegionNetworkAssemblyService>();
        await service.AssembleAsync(new NetworkAssemblyRequest(regionId, RefreshFromOsm: false));
        Console.WriteLine($"App assembly finished in {watch.Elapsed.TotalSeconds:F1}s");

        using (var scope = provider.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<OVDBDatabaseContext>();
            var networks = await db.RegionNetworks.Where(n => n.RegionId == regionId).OrderBy(n => n.Mode).ToListAsync();
            foreach (var n in networks)
            {
                var edges = await db.NetworkEdges.Where(e => e.RegionNetworkId == n.Id)
                    .Select(e => new { e.LengthKm, e.SampleCount, e.IsPassenger, HasTwins = e.HiddenTwins != null, HasStation = e.FromStation != null || e.ToStation != null })
                    .ToListAsync();
                Console.WriteLine($"=== {n.Mode} | {n.Status} | v{n.Version} | relations={n.ClassifiedByRelations} | osm {n.OsmDataTimestamp}");
                Console.WriteLine($"  {n.EdgeCount,6:N0} edges | passenger {n.TotalKm,8:N1} km | non-passenger {n.BonusKm,7:N1} km");
                Console.WriteLine($"  {edges.Count(e => e.HasTwins),6:N0} edges with hidden twins | {edges.Count(e => e.HasStation),6:N0} with a station label | {edges.Sum(e => e.SampleCount),8:N0} lattice samples | max edge {edges.Max(e => e.LengthKm):N2} km");
                if (!string.IsNullOrEmpty(n.Error))
                    Console.WriteLine($"  ERROR: {n.Error}");
            }
        }
        Console.WriteLine("Compare with the lab's run-<regionId>.log: passenger/non-passenger km must match (edge counts differ by design: 10 km cap vs the lab's 5 km).");
    }

    private sealed class NoOverpass : IOverpassService
    {
        public Task<string> QueryAsync(string query, CancellationToken cancellationToken = default) =>
            throw new InvalidOperationException("port-check must be served from the cache; an Overpass call means the cache seeding failed");
    }

    /// <summary>Prints SignalR progress to the console instead of broadcasting it.</summary>
    private sealed class ConsoleHubContext : IHubContext<MapGenerationHub>
    {
        public IHubClients Clients { get; } = new ConsoleClients();
        public IGroupManager Groups => throw new NotSupportedException();

        private sealed class ConsoleClients : IHubClients
        {
            private static readonly IClientProxy Proxy = new ConsoleProxy();
            public IClientProxy All => Proxy;
            public IClientProxy AllExcept(IReadOnlyList<string> excludedConnectionIds) => Proxy;
            public IClientProxy Client(string connectionId) => Proxy;
            public IClientProxy Clients(IReadOnlyList<string> connectionIds) => Proxy;
            public IClientProxy Group(string groupName) => Proxy;
            public IClientProxy GroupExcept(string groupName, IReadOnlyList<string> excludedConnectionIds) => Proxy;
            public IClientProxy Groups(IReadOnlyList<string> groupNames) => Proxy;
            public IClientProxy User(string userId) => Proxy;
            public IClientProxy Users(IReadOnlyList<string> userIds) => Proxy;
        }

        private sealed class ConsoleProxy : IClientProxy
        {
            public Task SendCoreAsync(string method, object[] args, CancellationToken cancellationToken = default)
            {
                Console.Write($"\r  {method}: {string.Join(' ', args)}   ");
                return Task.CompletedTask;
            }
        }
    }
}
