# Rail network completion: setup

What has to happen, once, to take the network completion feature (branch `network-completion`,
design in `docs/region-network-completion.md`) from merge to a working Network tab in
production. Nothing here is automatic except the migration.

## 1. Before deploying

1. **Dump the production database.** Startup applies migration
   `20261005202747_AddRegionNetworkCompletion` automatically. It adds four tables
   (`RegionNetworks`, `NetworkEdges`, `RouteNetworkEdges`, `NetworkWayOverrides`), the column
   `RouteTypes.MatchesRailNetworks`, and seeds that column once. It cannot be undone by
   re-pointing the image.
2. **Know what the seed does.** It sets `MatchesRailNetworks = 1` on route types that run on
   rails. Where `TrainlogType` is set, it alone decides: `train`, `rail`, `tram` and `metro`
   count. Otherwise `IsTrain` or a rail-sounding name decides (trein, train, zug, rail, tram,
   metro, subway, u-bahn, s-bahn, stadtbahn, light rail), and a name containing "bus" never
   counts. On the dev copy of production it flags 27 of 71 types. Check the result afterwards
   in the route types screen: every type now has a "Runs on rails" checkbox, and flipping it
   rematches that type's routes.

## 2. Container configuration (NAS)

The Overpass responses are cached on disk, so curation and reassembly never call Overpass
again. The default location is `/app/network-cache` inside the container, which is lost on
every image update ("clear" in Container Manager). Give it a volume:

| Setting | Value |
|---|---|
| Volume | a NAS folder, for example `/volume1/docker/ovdb/network-cache`, mounted at `/data/network-cache` |
| Environment variable | `Networks__CachePath=/data/network-cache` |

The container user needs write access to that folder. Without the volume everything still
works, but every reassembly after an image update fetches from Overpass again (three queries,
up to 15 minutes each for a large region).

No other configuration is needed. Long Overpass queries use their own HTTP client
(`OSM-LongQuery`, 17-minute timeout); everything else keeps the existing `OSM` client.

## 3. First use, after the deploy

1. **Log in as an admin and open Administrator → Regions.**
2. **Assemble the Netherlands** with the route icon on its card ("Assemble network"). The
   chip shows progress: fetch 0–20 %, topology 20–80 %, publishing and matching everyone's
   routes 80–100 %. Expect a few minutes for the first fetch, then about a minute.
   The cloud icon ("Refresh from OpenStreetMap") does the same but refetches from Overpass;
   use it only when OSM has changed meaningfully.
3. **If the card shows "Network assembly failed"**, the error text is next to it. The usual
   cause is Overpass being overloaded: try again later. A failed run never takes a published
   network offline.
4. **Open Statistics → Network.** Pick the region and Rail or Urban rail. Reference numbers on
   the dev copy of production for the admin account: Rail 98.9 %, Urban rail 93.6 %.
5. **Curate.** As an admin, click a stretch on the map (or a "What to ride next" entry) and
   choose Exclude, Force rail or Force urban rail. The region reassembles from the cache and
   the map updates in place. Each action is one entry in the Corrections list with a single
   undo. Candidates seen so far: the Hoorn–Medemblik museum tram shows up as urban passenger
   track, and the Goes–Borsele museum line as unridden rail.
6. **Other regions** (for example Nordrhein-Westfalen) work the same way. Very large regions
   such as all of Germany are not supported yet: the single Overpass fetch is too big.

## 4. Good to know

- **After a restart mid-run**, the run is marked failed at the next startup ("Interrupted by
  an application restart"); assemble again. The published network stays online.
- **New and changed routes are matched automatically**: on creation, on a type change, when a
  type's rail checkbox flips, and when regions are reassigned. Admins can force a full
  rematch with `POST /api/networks/region/{regionId}/rematch`.
- **Region polygons decide what is in a network.** The Netherlands includes Aruba, so its
  tram is part of the Dutch urban network; the map fits to the main body and Aruba is a pan
  away.

## 5. Local development

`appsettings.Development.json` must point at the dev database (see CLAUDE.md). To rebuild the
dev networks from the lab's cached Overpass data without calling Overpass:

```powershell
cd tools/NetworkLab
dotnet run -c Release -- port-check 1   # 1 = Netherlands; refuses non-local connection strings
```

It runs the app's own assembly and matching with the app's exact database settings, then
prints the numbers to compare with the lab (`docs/region-network-completion.md` §11.1). When
running the app locally, set `Networks__CachePath` to `tools/NetworkLab/data/app-cache` to
reuse the same cache.
