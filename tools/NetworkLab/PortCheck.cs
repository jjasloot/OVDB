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
        services.AddSingleton<INetworkMatchIndexCache, NetworkMatchIndexCache>();
        services.AddScoped<INetworkMatchService, NetworkMatchService>();
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

        // The assembly service enqueued NetworkChanged items for the (absent) queue service; run
        // the matcher directly instead, exactly as the queue would.
        using (var scope = provider.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<OVDBDatabaseContext>();
            var matcher = scope.ServiceProvider.GetRequiredService<INetworkMatchService>();
            var networkIds = await db.RegionNetworks.Where(n => n.RegionId == regionId).Select(n => n.Id).ToListAsync();
            watch.Restart();
            foreach (var id in networkIds)
                await matcher.RematchNetworkAsync(id, p => { Console.Write($"\r  matching network {id}: {p}%   "); return Task.CompletedTask; });
            Console.WriteLine($"\rApp matching finished in {watch.Elapsed.TotalSeconds:F1}s");
        }

        using (var scope = provider.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<OVDBDatabaseContext>();
            var networks = await db.RegionNetworks.Where(n => n.RegionId == regionId).OrderBy(n => n.Mode).ToListAsync();
            foreach (var n in networks)
            {
                var edges = await db.NetworkEdges.Where(e => e.RegionNetworkId == n.Id)
                    .Select(e => new { e.Id, e.LengthKm, e.SampleCount, e.IsPassenger, HasTwins = e.HiddenTwins != null, HasStation = e.FromStation != null || e.ToStation != null })
                    .ToListAsync();
                Console.WriteLine($"=== {n.Mode} | {n.Status} | v{n.Version} | relations={n.ClassifiedByRelations} | osm {n.OsmDataTimestamp}");
                Console.WriteLine($"  {n.EdgeCount,6:N0} edges | passenger {n.TotalKm,8:N1} km | non-passenger {n.BonusKm,7:N1} km");
                Console.WriteLine($"  {edges.Count(e => e.HasTwins),6:N0} edges with hidden twins | {edges.Count(e => e.HasStation),6:N0} with a station label | {edges.Sum(e => e.SampleCount),8:N0} lattice samples | max edge {edges.Max(e => e.LengthKm):N2} km");
                if (!string.IsNullOrEmpty(n.Error))
                    Console.WriteLine($"  ERROR: {n.Error}");

                // Coverage the way the completion endpoint computes it, but over every instanced
                // route regardless of owner — the lab matched the whole dev database too.
                var rows = await db.RouteNetworkEdges
                    .Where(m => m.Edge.RegionNetworkId == n.Id && m.Route.RouteInstances.Any())
                    .Select(m => new { m.NetworkEdgeId, m.CoveredSamples })
                    .ToListAsync();
                var byEdge = edges.ToDictionary(e => e.Id);
                double covered = 0, bonus = 0;
                var partial = 0;
                foreach (var g in rows.GroupBy(r => r.NetworkEdgeId))
                {
                    var e = byEdge[g.Key];
                    var union = new bool[e.SampleCount];
                    foreach (var r in g)
                        NetworkLattice.OrInto(union, r.CoveredSamples);
                    var pop = NetworkLattice.PopCount(union);
                    if (pop < union.Length) partial++;
                    var km = e.LengthKm * pop / union.Length;
                    if (e.IsPassenger) covered += km; else bonus += km;
                }
                Console.WriteLine($"  coverage @15m (all instanced routes): {covered,8:N1} / {n.TotalKm:N1} km = {(n.TotalKm > 0 ? covered / n.TotalKm * 100 : 0):F1}% | bonus {bonus:N1} km | {rows.Count:N0} match rows | {partial:N0} partially covered edges");
            }
        }
        Console.WriteLine("Compare with the lab's run-<regionId>.log (15 m rows): km and percentages must match.");
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
