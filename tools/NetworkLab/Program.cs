using System.Diagnostics;
using Microsoft.EntityFrameworkCore;
using NetTopologySuite.Geometries;
using NetworkLab;
using OVDB_database.Database;

NetTopologySuite.NtsGeometryServices.Instance = new NetTopologySuite.NtsGeometryServices(GeometryOverlay.NG);

var connectionString = Environment.GetEnvironmentVariable("OVDB_DEV_DB")
    ?? "Server=127.0.0.1;Port=3307;Database=ovdb;Uid=ovdb;Pwd=ovdb-dev;";
var dataDir = Path.Combine(Directory.GetCurrentDirectory(), "data");
Directory.CreateDirectory(dataDir);

switch (args.FirstOrDefault())
{
    case "regions":
        await ListRegionsAsync(args.Skip(1).FirstOrDefault());
        break;
    case "run" when args.Length >= 2 && int.TryParse(args[1], out var regionId):
        await RunAsync(regionId);
        break;
    case "port-check" when args.Length >= 2 && int.TryParse(args[1], out var checkRegionId):
        await PortCheck.RunAsync(checkRegionId, connectionString, dataDir);
        break;
    default:
        Console.WriteLine("Usage:");
        Console.WriteLine("  dotnet run -- regions [nameFilter]   list regions with ids");
        Console.WriteLine("  dotnet run -- run <regionId>         fetch(cached) + assemble + match + export");
        Console.WriteLine("  dotnet run -- port-check <regionId>  run the APP's assembly service on the dev db from the lab's caches");
        return;
}

async Task ListRegionsAsync(string filter)
{
    await using var db = CreateDb();
    var regions = await db.Regions
        .Where(r => filter == null || r.Name.Contains(filter))
        .OrderBy(r => r.ParentRegionId.HasValue).ThenBy(r => r.Name)
        .Select(r => new { r.Id, r.Name, r.OsmRelationId, r.ParentRegionId })
        .Take(50)
        .ToListAsync();
    foreach (var r in regions)
        Console.WriteLine($"{r.Id,5}  {(r.ParentRegionId.HasValue ? "  sub" : "TOP  ")}  {r.Name}  (OSM {r.OsmRelationId})");
}

async Task RunAsync(int regionId)
{
    var totalWatch = Stopwatch.StartNew();
    await using var db = CreateDb();
    var region = await db.Regions
        .Where(r => r.Id == regionId)
        .Select(r => new { r.Id, r.Name, r.OsmRelationId, r.Geometry })
        .FirstOrDefaultAsync();
    if (region == null)
    {
        Console.WriteLine($"No region with id {regionId}");
        return;
    }
    Console.WriteLine($"Region: {region.Name} (OSM relation {region.OsmRelationId})");

    // 1. Fetch (once) + parse
    var cachePath = await OverpassClient.GetOrFetchAsync(dataDir, region.Id, region.OsmRelationId);
    var watch = Stopwatch.StartNew();
    var osmData = OsmRailParser.Parse(cachePath);
    Console.WriteLine($"Parsed {osmData.Ways.Count:N0} ways / {osmData.Nodes.Count:N0} nodes in {watch.Elapsed.TotalSeconds:F1}s");

    // 2. Assemble (the projection is shared by the parallel-track dedup and the matcher)
    watch.Restart();
    var lat0 = region.Geometry?.Centroid.Y ?? osmData.Nodes.Values.First().Lat;
    var projection = new Geo.LocalProjection(lat0);
    var stations = await db.Stations
        .Where(s => s.Regions.Any(r => r.Id == regionId))
        .Select(s => new Coordinate(s.Longitude, s.Lattitude))
        .ToListAsync();
    var stopsPath = await OverpassClient.GetOrFetchStopsAsync(dataDir, region.Id, region.OsmRelationId);
    var urbanStops = OsmRailParser.Parse(stopsPath).Nodes.Values
        .Select(n => new Coordinate(n.Lon, n.Lat))
        .ToList();
    var routesPath = await OverpassClient.GetOrFetchRoutesAsync(dataDir, region.Id, region.OsmRelationId);
    var ptServices = PtRouteParser.Parse(routesPath);
    var servedWays = osmData.Ways.Count(w => ptServices.ContainsKey(w.Id));
    Console.WriteLine($"Edge splitting: {stations.Count:N0} rail stations (db), {urbanStops.Count:N0} tram/metro stops (osm)");
    Console.WriteLine($"PT relations: {ptServices.Count:N0} served ways total, {servedWays:N0} of this region's {osmData.Ways.Count:N0} rail ways carry passenger service");
    var networks = TopologyBuilder.Build(osmData, region.Geometry, projection, stations, urbanStops, ptServices);
    Console.WriteLine($"Assembled in {watch.Elapsed.TotalSeconds:F1}s:");
    foreach (var (mode, edges) in networks.OrderBy(n => n.Key))
    {
        var passengerKm = edges.Where(e => e.IsPassenger).Sum(e => e.LengthKm);
        var otherKm = edges.Where(e => !e.IsPassenger).Sum(e => e.LengthKm);
        Console.WriteLine($"  {mode,-10} {edges.Count,6:N0} edges | passenger {passengerKm,8:N1} km | non-passenger {otherKm,7:N1} km");
    }

    // 3. Match
    watch.Restart();
    var indexes = networks.ToDictionary(n => n.Key, n => Matcher.BuildIndex(n.Value, projection));
    Console.WriteLine($"Built match indexes in {watch.Elapsed.TotalSeconds:F1}s");

    watch.Restart();
    var routeCount = 0;
    var eligibleCount = 0;
    const int pageSize = 500;
    for (var page = 0; ; page++)
    {
        var routes = await db.Routes
            .Where(r => r.Regions.Any(reg => reg.Id == regionId) && r.RouteInstances.Any())
            .OrderBy(r => r.RouteId)
            .Skip(page * pageSize).Take(pageSize)
            .Select(r => new
            {
                r.RouteId,
                r.Name,
                r.LineString,
                r.TrainlogType,
                TypeTrainlogType = r.RouteType.TrainlogType,
                IsTrain = (bool?)r.RouteType.IsTrain,
                TypeName = r.RouteType.Name,
            })
            .ToListAsync();
        if (routes.Count == 0)
            break;

        foreach (var route in routes)
        {
            routeCount++;
            if (route.LineString == null || route.LineString.Coordinates.Length < 2)
                continue;
            var modes = Matcher.EligibleModes(route.TrainlogType, route.TypeTrainlogType, route.IsTrain, route.TypeName);
            if (modes.Count == 0)
                continue;
            eligibleCount++;
            var labRoute = new LabRoute(route.RouteId, route.Name, route.LineString, modes);
            foreach (var mode in modes)
            {
                if (indexes.TryGetValue(mode, out var index))
                    Matcher.MatchRoute(labRoute, index, projection);
            }
        }
        Console.Write($"\r  matched {routeCount:N0} routes ({eligibleCount:N0} eligible)...");
    }
    Console.WriteLine($"\r  matched {routeCount:N0} routes ({eligibleCount:N0} eligible) in {watch.Elapsed.TotalSeconds:F1}s");
    foreach (var index in indexes.Values)
        index.FinalizeCoverage();

    // 4. Report
    foreach (var (mode, edges) in networks.OrderBy(n => n.Key))
    {
        var passenger = edges.Where(e => e.IsPassenger).ToList();
        if (passenger.Count == 0 && edges.Count == 0)
            continue;
        var totalKm = passenger.Sum(e => e.LengthKm);
        Console.WriteLine($"\n=== {mode} | passenger network {totalKm:N1} km, {passenger.Count:N0} edges ===");
        Console.WriteLine($"  {"tol",5} | {"covered",12} | {"pct",6} | {"fractional",16} | {"bonus (non-pass.)",18} | gray zone (0.4-0.75)");
        for (var t = 0; t < Matcher.Tolerances.Length; t++)
        {
            var coveredKm = passenger.Where(e => e.BestFraction[t] >= Matcher.CoverFractionThreshold).Sum(e => e.LengthKm);
            // Sample-exact credit (v2 completion): fractions are already zero below the
            // MinStoredSamples storage floor, so no separate noise floor is needed here.
            var fractionalKm = passenger.Sum(e => e.LengthKm * e.BestFraction[t]);
            var bonusKm = edges.Where(e => !e.IsPassenger && e.BestFraction[t] >= Matcher.CoverFractionThreshold).Sum(e => e.LengthKm);
            var grayCount = passenger.Count(e => e.BestFraction[t] is >= 0.4 and < Matcher.CoverFractionThreshold);
            Console.WriteLine($"  {Matcher.Tolerances[t],4}m | {coveredKm,9:N1} km | {(totalKm > 0 ? coveredKm / totalKm * 100 : 0),5:F1}% | {fractionalKm,8:N1} = {(totalKm > 0 ? fractionalKm / totalKm * 100 : 0),4:F1}% | {bonusKm,15:N1} km | {grayCount,4} edges");
        }

        // Missing lines at 20 m (tolerance index 2), grouped by name.
        var t20 = Array.IndexOf(Matcher.Tolerances, 20d);
        var missing = passenger
            .Where(e => e.BestFraction[t20] < Matcher.CoverFractionThreshold && e.Name != null)
            .GroupBy(e => e.Name)
            .Select(g => new { Name = g.Key, Km = g.Sum(e => e.LengthKm) })
            .OrderByDescending(g => g.Km)
            .Take(15)
            .ToList();
        if (missing.Count > 0)
        {
            Console.WriteLine("  Biggest missing lines @20m:");
            foreach (var line in missing)
                Console.WriteLine($"    {line.Km,7:N1} km  {line.Name}");
        }

        GeoJsonExport.Write(dataDir, regionId, mode, edges);
    }

    Console.WriteLine($"\nTotal {totalWatch.Elapsed.TotalSeconds:F1}s. Open viewer.html and drop the geojson files on it.");
}

OVDBDatabaseContext CreateDb()
{
    var options = new DbContextOptionsBuilder<OVDBDatabaseContext>()
        .UseMySql(connectionString, ServerVersion.AutoDetect(connectionString), o => o.UseNetTopologySuite())
        .Options;
    return new OVDBDatabaseContext(options);
}
