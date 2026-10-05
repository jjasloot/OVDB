using System.Text;

namespace NetworkLab;

/// <summary>
/// Fetches the raw rail data for a region from Overpass exactly once and caches it on disk.
/// Every later run reads the cached file; delete it manually to force a refetch.
/// </summary>
public static class OverpassClient
{
    // Same prioritized list as OV_DB.Services.OverpassService.
    private static readonly string[] Mirrors =
    [
        "https://overpass.kumi.systems/api/interpreter",
        "https://overpass-api.de/api/interpreter",
        "https://overpass.openstreetmap.fr/api/interpreter",
        "https://overpass.private.coffee/api/interpreter",
    ];

    public static string CachePath(string dataDir, int regionId) =>
        Path.Combine(dataDir, $"overpass-region-{regionId}.json");

    public static string StopsCachePath(string dataDir, int regionId) =>
        Path.Combine(dataDir, $"overpass-region-{regionId}-stops.json");

    /// <summary>
    /// Tram stops and metro/light-rail stations, for splitting UrbanRail edges: the OVDB
    /// Stations table deliberately excludes subway and tram, so OSM is the source here.
    /// Cached separately from (and much smaller than) the main rail fetch.
    /// </summary>
    private static string BuildStopsQuery(long osmRelationId) =>
        $"[out:json][timeout:300];" +
        $"area({osmRelationId + 3_600_000_000})->.searchArea;" +
        "(node[\"railway\"=\"tram_stop\"](area.searchArea);" +
        "node[\"railway\"=\"station\"][\"station\"=\"subway\"](area.searchArea);" +
        "node[\"railway\"=\"station\"][\"station\"=\"light_rail\"](area.searchArea););" +
        "out body;";

    public static Task<string> GetOrFetchStopsAsync(string dataDir, int regionId, long osmRelationId) =>
        GetOrFetchCoreAsync(StopsCachePath(dataDir, regionId), BuildStopsQuery(osmRelationId));

    public static string RoutesCachePath(string dataDir, int regionId) =>
        Path.Combine(dataDir, $"overpass-region-{regionId}-routes.json");

    /// <summary>
    /// Public-transport route relations, member lists only (no geometry): the service-based
    /// source for the passenger flag and for rail-vs-urban assignment of shared tracks.
    /// </summary>
    private static string BuildRoutesQuery(long osmRelationId) =>
        $"[out:json][timeout:600];" +
        $"area({osmRelationId + 3_600_000_000})->.searchArea;" +
        "relation[\"route\"~\"^(train|light_rail|subway|tram)$\"](area.searchArea);" +
        "out body;";

    public static Task<string> GetOrFetchRoutesAsync(string dataDir, int regionId, long osmRelationId) =>
        GetOrFetchCoreAsync(RoutesCachePath(dataDir, regionId), BuildRoutesQuery(osmRelationId));

    /// <summary>
    /// All rail-bound modes, no service tracks (sidings/yards/spurs/crossovers). Freight and
    /// other non-passenger lines are deliberately INCLUDED here; the topology builder flags
    /// them per way so the passenger/bonus split is a processing decision, not a fetch decision.
    /// </summary>
    private static string BuildQuery(long osmRelationId) =>
        $"[out:json][timeout:900];" +
        $"area({osmRelationId + 3_600_000_000})->.searchArea;" +
        "(way[\"railway\"~\"^(rail|narrow_gauge|preserved|light_rail|subway|tram)$\"][!\"service\"](area.searchArea););" +
        "out body;>;out skel qt;";

    public static async Task<string> GetOrFetchAsync(string dataDir, int regionId, long osmRelationId)
    {
        var path = CachePath(dataDir, regionId);
        return await GetOrFetchCoreAsync(path, BuildQuery(osmRelationId));
    }

    private static async Task<string> GetOrFetchCoreAsync(string path, string query)
    {
        if (File.Exists(path))
        {
            Console.WriteLine($"Using cached Overpass data: {path} ({new FileInfo(path).Length / 1024 / 1024} MB)");
            return path;
        }

        Directory.CreateDirectory(Path.GetDirectoryName(path));
        using var client = new HttpClient { Timeout = TimeSpan.FromMinutes(20) };
        client.DefaultRequestHeaders.UserAgent.ParseAdd("OVDB-NetworkLab/1.0 (ovdb.infinityx.nl)");

        for (var round = 1; round <= 3; round++)
        {
            if (round > 1)
            {
                Console.WriteLine($"All mirrors failed in round {round - 1}; waiting {30 * (round - 1)}s before retrying...");
                await Task.Delay(TimeSpan.FromSeconds(30 * (round - 1)));
            }
            foreach (var mirror in Mirrors)
            {
            Console.WriteLine($"Querying Overpass ({mirror}) - this is the one and only fetch, result is cached...");
            try
            {
                using var response = await client.PostAsync(mirror, new StringContent("data=" + Uri.EscapeDataString(query), Encoding.UTF8, "application/x-www-form-urlencoded"));
                if (!response.IsSuccessStatusCode)
                {
                    Console.WriteLine($"  {(int)response.StatusCode} {response.ReasonPhrase}, trying next mirror");
                    continue;
                }

                var tempPath = path + ".tmp";
                await using (var target = File.Create(tempPath))
                {
                    await response.Content.CopyToAsync(target);
                }

                // Cheap sanity check so a truncated download is never cached as the one fetch.
                if (!LooksComplete(tempPath))
                {
                    Console.WriteLine("  response looks truncated/invalid, trying next mirror");
                    File.Delete(tempPath);
                    continue;
                }

                File.Move(tempPath, path);
                Console.WriteLine($"Cached {new FileInfo(path).Length / 1024 / 1024} MB to {path}");
                return path;
            }
            catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or IOException)
            {
                Console.WriteLine($"  failed: {ex.Message}, trying next mirror");
            }
            }
        }

        throw new InvalidOperationException("All Overpass mirrors failed in all rounds; nothing was cached.");
    }

    private static bool LooksComplete(string path)
    {
        using var stream = File.OpenRead(path);
        if (stream.Length < 1024)
            return false;

        var head = new byte[4096];
        var headRead = stream.Read(head, 0, head.Length);
        if (!Encoding.UTF8.GetString(head, 0, headRead).Contains("\"elements\""))
            return false;

        stream.Seek(-Math.Min(64, stream.Length), SeekOrigin.End);
        var tail = new byte[64];
        var tailRead = stream.Read(tail, 0, tail.Length);
        return Encoding.UTF8.GetString(tail, 0, tailRead).TrimEnd().EndsWith("}");
    }
}
