# Region rail network completion — architecture v2

Show, per region, the passenger rail network with every ridden metre highlighted — green up to
the exact point where you got off — plus an exact percentage by route-kilometre. Like Trainlog's
country percent, but line-based instead of area-blob-based, computed from OVDB's own route
geometries, and defined by what actually carries passenger service.

This is the second architecture. The first (Aug 2026) was validated end to end in
`tools/NetworkLab` on the Netherlands and North Rhine-Westphalia; section 11 is the evidence.
v2 keeps every algorithm that survived that validation and removes the one assumption that kept
needing patches: that an edge is the atomic unit of coverage.

## 1. Principles

1. **Coverage lives on samples, not edges.** The network is a lattice of 25 m samples along
   centerline geometry. A ride covers samples; coverage is the union of all rides per sample;
   the percentage is covered sample-length over total. Edges exist to carry geometry, a name
   and a station span — they are *containers*, never a binary covered/uncovered unit. There is
   no coverage threshold anywhere in the system.
2. **Service is truth; tags are fallback.** Whether a track is passenger network, and whether
   it is rail or urban rail, comes from OSM public-transport route relations. Way tags
   (`usage`, `railway:traffic_mode`) are the fallback for regions with poor relation coverage.
3. **Every rail-bound ride covers both networks.** The Rail/UrbanRail split organizes the
   denominator; geometry decides what a ride matches. A tram-typed ride over railway track
   (Köln line 7 to Benzelrath) covers it.
4. **The percentage never exceeds 100.** Only passenger-service track is in the denominator.
   Ridden non-passenger track (freight lines, museum lines without relations, depots) is shown
   and reported separately as *bonus km* — the station analogy: riding past counts, but it
   doesn't inflate a completion figure.
5. **Union across rides is mandatory.** Rides logged in legs, and arrive-then-depart pairs at a
   station, each cover part of a stretch; only their union is the truth.
6. **Curation is first-class.** OSM is never perfect; any way can be forced into Rail, into
   UrbanRail, or out of the network, keyed by stable OSM way id so it survives reassembly.
7. **Assembly may be slow, reading must be fast.** Assembly is a queued background job per
   region; matching is incremental per route; the read path is indexed joins plus bit
   operations on small bitmaps.

## 2. Concepts

| Term | Meaning |
|---|---|
| **Network** | One `(Region, Mode)` pair; `Mode ∈ {Rail, UrbanRail}`. Assembled from OSM, stored as edges. |
| **Edge** | A stretch of deduplicated centerline between two break points (junction, passenger-flag change, override boundary, station), capped at 10 km purely to bound bitmap size. Carries geometry, length, name, a from/to station label, hidden twin geometry, and `SampleCount`. |
| **Sample** | A point every 25 m along an edge (vertices included, gaps ≤ 25 m). The unit of coverage. |
| **Match** | A per-`(Route, Edge)` row holding a bitmap: one bit per sample the route passes within tolerance. |
| **Coverage** | Per sample: the OR of the bitmaps of all the user's instanced routes. |
| **Completion** | `Σ covered sample-length (passenger edges) / Σ passenger length`, exact. |
| **Bonus** | Covered sample-length on non-passenger edges. Reported next to, never inside, the percentage. |
| **Override** | A per-way curation record: Excluded, ForceRail or ForceUrbanRail. |

Networks are assembled independently per region (a country and its provinces each get their own
edges). Duplication is accepted for self-containment; see Appendix B for the deferred
alternative.

## 3. Data model

```csharp
public enum NetworkMode { Rail = 0, UrbanRail = 1 }
public enum NetworkStatus { Queued = 0, Assembling = 1, Ready = 2, Failed = 3 }
public enum NetworkOverrideKind { Excluded = 0, ForceRail = 1, ForceUrbanRail = 2 }

[Index(nameof(RegionId), nameof(Mode), IsUnique = true)]
public class RegionNetwork
{
    [Key] public int Id { get; set; }
    public int RegionId { get; set; }
    public Region Region { get; set; }
    public NetworkMode Mode { get; set; }
    public NetworkStatus Status { get; set; }
    public DateTime? AssembledAt { get; set; }
    public string OsmDataTimestamp { get; set; }      // osm3s.timestamp_osm_base, for display
    public int EdgeCount { get; set; }
    /// App-managed; the ETag of the geometry payload. Bumped only on reassembly. (MariaDB has
    /// no rowversion type; an auto-updating TIMESTAMP would change on any row touch.)
    public int Version { get; set; }
    public double TotalKm { get; set; }               // passenger length = denominator
    public double BonusKm { get; set; }               // non-passenger length, never in the denominator
    public bool ClassifiedByRelations { get; set; }   // false = tag-heuristic fallback was used
    public string Error { get; set; }
    public List<NetworkEdge> Edges { get; set; }
}

public class NetworkEdge
{
    [Key] public long Id { get; set; }
    public int RegionNetworkId { get; set; }
    public RegionNetwork Network { get; set; }         // cascade delete
    public LineString Geometry { get; set; }           // full centerline, for matching
    /// Douglas-Peucker ~10 m, for display. Keeps a subset of Geometry's own vertices, which is
    /// what lets completion intervals be mapped onto its chainage exactly (section 6).
    public LineString SimplifiedGeometry { get; set; }
    /// Physical tracks hidden by the parallel dedup, attached to this survivor; the matcher
    /// probes them so riding either track of a corridor covers the corridor. Never displayed.
    public MultiLineString HiddenTwins { get; set; }
    public double LengthKm { get; set; }               // geodesic, no fudge factor
    public int SampleCount { get; set; }               // ceil(length / 25 m) + 1, fixed at assembly
    public bool IsPassenger { get; set; }              // from route relations (or fallback heuristic)
    public string Name { get; set; }                   // dominant OSM name along the edge
    public string FromStation { get; set; }            // set when the edge starts at a station split
    public string ToStation { get; set; }              // set when the edge ends at a station split
    public string OsmWayIds { get; set; }              // JSON array: curation key and debugging
}

[PrimaryKey(nameof(RouteId), nameof(NetworkEdgeId))]
[Index(nameof(NetworkEdgeId))]
public class RouteNetworkEdge
{
    public int RouteId { get; set; }
    public Route Route { get; set; }                   // cascade delete
    public long NetworkEdgeId { get; set; }
    public NetworkEdge Edge { get; set; }              // cascade delete
    /// One bit per sample of the edge (≤ 402 bits for a 10 km edge → ≤ 51 bytes), set where the
    /// route is within tolerance of the sample or of a hidden-twin probe. Rows exist when at
    /// least 4 samples (~100 m) are set — a perpendicular crossing touches 1–2 and stores nothing.
    public byte[] CoveredSamples { get; set; }
    public int CoveredCount { get; set; }              // popcount, for cheap aggregation
}

public class NetworkWayOverride
{
    [Key] public long Id { get; set; }
    public int RegionId { get; set; }                  // per region, all its modes
    public long OsmWayId { get; set; }
    public NetworkOverrideKind Kind { get; set; }
    public DateTime CreatedAt { get; set; }
}
```

Plus one field on the existing `RouteType`:

```csharp
/// Routes of this type run on rails and can cover network edges. OVDB-native: the matcher
/// must not depend on TrainlogType, which is Trainlog's export vocabulary, nor on IsTrain,
/// which means "train" specifically for statistics (tram and metro types have it off).
public bool MatchesRailNetworks { get; set; }
```

Seeded once by migration (`IsTrain`, or `TrainlogType ∈ {train, tram, metro}`, or a name
containing trein/train/zug/rail/tram/metro/subway/u-bahn, case-insensitive) and editable as a
checkbox in the route-types UI. Routes with no type, or a type with the flag off (bus, ferry,
walk), never match — a rail-replacement bus drawn along the railway covers nothing.

Cascade deletes do all invalidation: reassembling a network deletes its edges and thereby their
match rows; deleting a route deletes its match rows. One migration adds the four tables and the
flag.

## 4. Assembly pipeline

Hosted service `RegionNetworkAssemblyService`, modelled on `UpdateRegionService`: a static
`ConcurrentQueue<int>` of region ids, a loop with per-item try/catch so one failure never kills
the queue, progress over a new `MapGenerationHub.NetworkUpdateMethod = "RefreshNetwork"`
(regionId, mode, 0–100). One job assembles both modes of a region.

### 4.1 Three Overpass fetches, each cached

All through `IOverpassService.QueryAsync` (mirror failover) with the `UpdateRegionService`
retry pattern (3 attempts, 30 s × attempt). Area id = `OsmRelationId + 3_600_000_000`. The raw
responses are cached on disk per region (a configured path on the NAS volume, e.g.
`Networks:CachePath`, kept indefinitely — tens of MB per region); reassembly after a curation
change re-runs topology from the cache, and only an explicit "refresh from OSM" refetches.

1. **Track** — every rail-bound way, no usage filter, service tracks excluded:
   ```
   way["railway"~"^(rail|narrow_gauge|preserved|light_rail|subway|tram)$"][!"service"](area);
   out body; >; out skel qt;
   ```
   `[!"service"]` drops yards, sidings, spurs and crossovers — the main noise source. Rack
   railways are `rail`/`narrow_gauge` + `rack=*` and need nothing special. Funicular, monorail
   and ferries are out of scope (each would be a new mode). NL ≈ 32 MB, NRW ≈ 36 MB.
2. **Stops** — split points for urban rail (the `Stations` table deliberately excludes subway
   and tram): `node["railway"="tram_stop"]`, `node["railway"="station"]["station"="subway"]`,
   `node["railway"="station"]["station"="light_rail"]`. < 1 MB.
3. **Route relations** — member lists only, no geometry:
   `relation["route"~"^(train|light_rail|subway|tram)$"](area); out body;`. NL 7 MB, NRW 12 MB.

Parse all three streaming with `JsonTextReader` (never a DOM of a 36 MB response), keeping only
node coordinates, way node-lists with `railway`/`usage`/`railway:traffic_mode`/`name`, stop
coordinates, and a `Dictionary<long wayId, PtService>` (flags Train, Urban) from relation
members with track roles (empty, forward, backward, alternate, main — not platform/stop).

**Large regions:** a Germany-scale fetch is hundreds of MB. Fetch per sub-region
(`Region.SubRegions`), merge with way-id dedup, clip to the parent polygon. Implement when the
first big country is wanted; NL/BE/CH-scale regions and all sub-regions fetch directly.

### 4.2 Classification (per way, before any topology)

```
mode      = railway ∈ {rail, narrow_gauge, preserved} ? Rail : UrbanRail
pt        = ptServices[wayId]                         // None | Train | Urban | Train|Urban
if relations cover the region (served ways ≥ RelationCoverageFloor of rail ways —
                                 a tunable guess at 30 %; measured 85 % NL, 74 % NRW):
    passenger = pt != None
    if mode == Rail && pt == Urban: mode = UrbanRail  // railway track served only by Stadtbahn
else:                                                 // fallback: tag heuristic
    passenger = usage ∉ {freight, industrial, military, test} && railway:traffic_mode != freight
override  = overrides[wayId]
    Excluded       → drop the way
    ForceRail      → mode = Rail
    ForceUrbanRail → mode = UrbanRail
```

Relations are the truth where they exist: untagged freight lines (WLE, Tecklenburger Nordbahn)
and draisines have no passenger relation and demote to non-passenger — out of the denominator,
still visible, still bonus when ridden. The failure mode is graceful: a line whose relation is
missing in OSM stops inflating the percentage rather than showing red forever. The
`ClassifiedByRelations` flag on the network records which branch ran.

### 4.3 Topology per mode

Over that mode's ways only (a node shared by a tram way and a rail way is not a junction in
either network):

- **Junctions:** count node usage across ways (a node used twice anywhere, including twice
  within one way for a balloon loop, is a meeting point); split each way at *interior* meeting
  nodes into primitive segments.
- **Break nodes:** degree ≠ 2, or the two incident segments differ in passenger flag or in
  override — a line continuing as freight-only, or into an excluded stretch, must not merge
  through.
- **Chain walk:** start at every break-node end and merge through degree-2 nodes into maximal
  edges; a second pass sweeps pure cycles. Carry way ids and a name-by-length tally; the
  edge's `Name` is the dominant name.

### 4.4 Clip to the region polygon

`PreparedGeometryFactory.Prepare(region.Geometry)` once per job; `Contains` fast path,
`Intersection` for border edges, explode to pieces, drop slivers < 100 m.

### 4.5 Parallel-track dedup (the lab-validated algorithm, one proposed addition)

Parallel tracks of one line clutter the overview and double the km. No centerline synthesis;
instead, per sample:

- Priority-order edges: longer wins, index breaks ties (a total order — no cycles for 3–4 tracks).
- A sample is a **shadow** when a higher-priority edge with the same passenger flag and no
  **veto** runs bearing-aligned (≤ 20°, mod 180) within *hideDist* — Rail 25 m, UrbanRail 35 m
  (street-tram direction pairs sit further apart).
- **Veto (validated default): both named and names differ.** This is what protected S-Bahn/
  mainline pairs in NL and NRW while letting lines that genuinely share a corridor
  (Utrecht–Lunetten) merge visually.
- **Veto (proposed, not validated): disjoint *line identity*.** Relations could make the veto
  reliable where names are missing — but compared at the right level. OSM route relations are
  usually *per direction*, each containing only its own track, so "disjoint relation ids" would
  un-merge every double-track line and collapse dedup back to track-km. Compare instead by line
  identity: the serving `route_master` relations (add them to fetch 3) or the serving
  relations' `ref`/`name`. Adopt only after the lab shows NL route-km unchanged (~2,760) with
  the veto on; until then the name veto stands alone.
- **Centerline averaging:** every visible sample moves to the mean of itself and its aligned
  lower-priority partners within 15 m, so a track pair renders as one line down the corridor
  middle and side-hops at transitions self-heal. The 15 m cap (tighter than hideDist) stops
  tram pairs on parallel streets from centerlining through the block between them.
- **Smoothing:** flip runs < 150 m to their surroundings; flip interior *visible* runs < 1 km
  that have shadow on both sides (corridor flicker — a partner weaving across hideDist as a
  shared right-of-way narrows and widens; a genuine divergence ends at a junction instead).
- Cut along the sample chain: visible runs (averaged) become edges; shadow runs (raw physical
  geometry) become **hidden twins**. Twins are attached to the nearest surviving edge only
  **after 4.6 and 4.7 have made their cuts** — attaching earlier would bind them to edges that
  no longer exist.

### 4.6 Station and stop splitting — display structure

Snap every rail station (DB `Stations` of the region) onto every surviving Rail edge within
75 m, and every OSM stop onto every UrbanRail edge within 75 m; split at the nearest point, no
split within 100 m of an edge end or another split. Record the station name on the resulting
edges' `FromStation`/`ToStation`.

In v1 this made rides end on edge boundaries so binary coverage could be right. In v2 coverage
is per sample and needs none of that; splitting remains because it gives every edge, and so
every uncovered run, a human label ("Utrecht Centraal – Bunnik" rather than "4.3 km unnamed"),
and because it stops edge names bleeding through stations (the "Goudse lijn" that continued
southeast past Utrecht CS).

### 4.7 Length cap — storage

Edges longer than 10 km are cut into equal parts with interpolated break points. This bounds
the per-edge bitmap at 401 bits; it has no coverage meaning (v1's 5 km chunking did, and is
gone).

### 4.8 Finishing

- `LengthKm`: haversine over the coordinates (extract `DistanceCalculationHelper`'s private
  formula into a shared helper; no ×1.0064 — that is a route-import calibration).
- **The match lattice — the stored count is the contract.** Assembly computes
  `SampleCount = floor(L / 25) + 1` (plus one when `L` is not a multiple of 25 m) from the
  projected length and stores it. From then on sample *i* sits at chainage `i · L / (n − 1)`
  and has fraction `i / (n − 1)`; nothing downstream depends on the projection or on 25 m being
  exact. This matters because the projection is anchored at the region centroid and
  `Region.Geometry` is rewritten on every region re-import — a count recomputed from a moved
  centroid could shift bit positions under stored bitmaps. (`NetworkLattice` in the app; the
  lab's per-segment `Sample()` inside dedup and splitting is a different, assembly-internal
  sampler and must not be reused for matching.)
- `SimplifiedGeometry`: Douglas-Peucker 0.0001° (~10 m), endpoints preserved by construction.
- `TotalKm`/`BonusKm`/`EdgeCount` on the network row.

### 4.9 Persistence and rematch

Per `(region, mode)` in one transaction: delete old edges (cascade removes stale matches),
insert new ones in batches of ~1000 with a cleared change tracker, update the network row with
`Version++`, `Status = Ready`. Then enqueue `NetworkChanged`. Progress: fetch 0→20, topology
20→45, clip+dedup+split 45→80, persist 80→100.

## 5. Matching

Service `NetworkMatchService` plus a hosted queue `NetworkMatchQueueService` with two work items:
`RouteChanged(routeId)` and `NetworkChanged(regionNetworkId)`.

### 5.1 Constants

| Constant | Value | Why |
|---|---|---|
| `ToleranceM` | **15 m** | Routes are wizard-drawn on OSM rail geometry, so they are track-accurate; twin probes make coverage tolerance-flat, so legitimate rides no longer need width, while freight-parallel false positives still grow with it (NRW bonus 207 km @10 m → 261 km @30 m). |
| `SampleSpacingM` | 25 m | The lattice. Shared by assembly (`SampleCount`) and matching. |
| `MinStoredSamples` | 4 | Rows exist from ~100 m of coverage; crossings store nothing. |

No coverage threshold.

### 5.2 Matching one route against one network

Everything runs in a **local equirectangular frame** (lat₀ = network centroid; x·cos lat₀·111320,
y·111320) so NTS planar distances are metres.

```
if !route.RouteType.MatchesRailNetworks: nothing to do
R   = route.LineString projected; ifd = IndexedFacetDistance(R)
candidates = edges whose envelope meets any 5 km window of R expanded by ToleranceM
             (never the whole route envelope: a cross-country route's box covers half the net)
for each candidate edge e:
    bits = new bit[e.SampleCount]                       // the chainage lattice of 4.8, not per-segment
    for each sample s of e (and its twin probes: nearest point on each hidden twin within 80 m):
        d = min distance from s / its probes to R
        bits[s] = d <= ToleranceM
    if popcount(bits) >= MinStoredSamples: upsert RouteNetworkEdge(route, e, bits)
```

Per-network projected edges, STRtree, samples and probes are built once per `NetworkChanged`
job and reused across routes.

### 5.3 Work items

`RouteChanged`: delete the route's rows; for each Ready network whose region is in
`route.Regions` (maintained by `RouteRegionsService`), match and insert. Enqueued from the
four call sites of `AssignRegionsToRouteAsync` (`RoutesController` ×2, `ImporterController`,
`AdminController`), after region assignment, and when a route's type changes.

`NetworkChanged`: rows are already gone (cascade); load the network once; iterate routes in
`route.Regions ∋ region` with `MatchesRailNetworks`, in pages of a few hundred (13k routes ≈
380 MB of geometry if materialized at once); insert in batches; report progress.

## 6. Read path

`NetworkCompletionController` (`/api/networks`), all `[Authorize]`:

- **`GET /api/networks/regions`** — regions with Ready networks, per mode: `totalKm`,
  `bonusKmTotal`, `edgeCount`, `assembledAt`, `classifiedByRelations`, and the caller's
  `coveredKm`. Drives the picker.
- **`GET /api/networks/{regionId}/{mode}/geometry`** — static: `{ version, totalKm, edges:
  [{ id, name, fromStation, toStation, lengthKm, isPassenger, geometry }] }` with simplified
  GeoJSON; `ETag: "{Version}"`, `Cache-Control: private`. NL rail ≈ 1–2 MB raw, ~300 KB gzipped.
- **`GET /api/networks/{regionId}/{mode}/completion?from=&to=`** — the per-user part:

  ```csharp
  var rows = await db.RouteNetworkEdges
      .Where(m => m.Edge.RegionNetworkId == networkId)
      .Where(m => m.Route.RouteMaps.Any(rm => rm.Map.UserId == userId))
      .Where(m => m.Route.RouteInstances.Any(ri =>
          (from == null || ri.Date >= from) && (to == null || ri.Date <= to)))
      .Select(m => new { m.NetworkEdgeId, m.CoveredSamples, m.Edge.SampleCount, m.Edge.LengthKm, m.Edge.IsPassenger })
      .ToListAsync();
  // per edge: union = OR of bitmaps; coveredKm += LengthKm * popcount(union) / SampleCount
  // intervals = run-length encode union into [start, end] fractions of the FULL geometry's
  //             length (a run a..b spans fraction (a − ½)/(n − 1) .. (b + ½)/(n − 1), so a
  //             fully covered edge is exactly [0, 1]), then re-expressed as fractions of the
  //             SIMPLIFIED geometry's length via the kept vertices (SimplifiedChainage), 3 decimals
  // → { percent, coveredKm, totalKm, bonusKm,
  //     edges: [{ id, intervals: [[0.000, 0.412], [0.655, 1.000]] }] }   // only edges with coverage
  ```

  Indexed joins plus ORing a few thousand ≤ 51-byte bitmaps: tens of milliseconds. No cache
  table unless proven necessary. Nothing bit-shaped reaches the browser: the client cuts the
  simplified polyline by length fraction, and because the fractions were mapped through the
  vertices the simplifier kept, the cut lands exactly where the ride ended even on edges whose
  simplified length differs from the full one by several percent (measured worst case 94 %).

- **`GET /api/networks/{regionId}/{mode}/missing`** — uncovered runs ≥ 500 m, grouped by edge
  name and station span, summed km, descending: the "what should I ride next" list.
- **Curation:** `GET/POST/DELETE /api/networks/region/{regionId}/overrides` (admin); a POST
  enqueues reassembly of that region from the cached fetch.
- **Admin:** `POST /api/networks/region/{regionId}/assemble` (`refresh=true` refetches OSM);
  `GET …/status`.

**Percent semantics:** `coveredKm / totalKm` over passenger edges only, exact to the sample,
one decimal, never rounded to 100.0 unless every passenger sample is covered. Bonus km shown
beside it ("+ 152 km outside the passenger network").

## 7. Frontend

New stats page `stats/network-completion` beside `region-stats` and `station-completion`:

- **Pickers:** region (Ready networks, grouped by country), mode tabs, the existing date/year
  filter pattern.
- **Map:** Leaflet with the existing base layers. From the geometry payload draw every edge thin
  (passenger: red; non-passenger: faint dashed). From the completion payload, cut each listed
  edge's simplified polyline at its interval fractions (walk cumulative length) and draw the
  covered pieces thick on top — passenger green, non-passenger teal dashed. Green ends exactly
  where the ride ended. Geometry cached client-side by `(region, mode, version)`; a filter
  change refetches only the small completion payload.
- **Header:** percent (one decimal), covered / total km, bonus km, "classified by route
  relations" or "by tags" as a small caveat badge.
- **Missing list** under the map from the `missing` endpoint, each entry zooming the map to it.
- **Curation (admin):** clicking an edge opens its ways with the current classification; three
  actions — Exclude, Force Rail, Force UrbanRail — plus Reset; applying enqueues reassembly with
  live progress. A per-region overrides list to review and undo.
- **Admin:** "assemble network" / "refresh from OSM" per region, progress via `RefreshNetwork`.
- i18n keys in `en.json` and `nl.json`.

## 8. Testing (OV_DB.Tests)

Topology (pure functions over synthetic ways): chain → one edge; T → three; X → four; way-end
touching an interior → split; balloon loop → neck is a junction; passenger-flag and override
boundaries are break nodes; clipping drops slivers; 10 km cap preserves total length.

Classification: served way → passenger; unserved → not; rail way with only urban relations →
UrbanRail; override wins; fallback heuristic when relations are sparse.

Dedup: two parallel tracks 6 m apart → one averaged edge + one hidden twin; distinct named
lines 20 m apart → two edges; a 300 m visible gap between shadows is flipped; a 2 km one is
not. If the proposed line-identity veto is adopted: two tracks served by per-direction
relations of the same `route_master` still merge.

Lattice: `SampleCount` from the chainage formula equals the matcher's bit count for edges whose
length is and is not a multiple of 25 m; interval fractions round-trip to the right chainage.

Matching: parallel at 5 m → all bits; at 40 m → none; perpendicular crossing → < 4 bits,
no row; ride on the hidden twin → bits set via probes; two routes covering halves → union
complete; type without `MatchesRailNetworks` → nothing.

Read path: intervals from a bitmap `11110000111` → `[[0,0.4],[0.8,1]]`-style pairs; coveredKm
exact; only instanced, owned, date-filtered routes count; shared edge counted once.

## 9. Sizing

NL rail: 2,762 passenger route-km after dedup → ~1,700 edges post-split, ~110k samples.
Assembly ~45 s in the lab (dedup dominates), of which the one-time fetch is excluded; NRW ~100 s.
Matching 2,735 eligible routes against NL in ~7 s, 902 against NRW in ~13 s. Match rows: tens
of thousands per network at ≤ 51 bytes each. Geometry payload ~300 KB gzipped; completion
payload a few KB.

## 10. Rollout

1. Migration: four tables, `RouteType.MatchesRailNetworks` with its seed. Entities, assembly
   service, three cached fetches, classification, topology, dedup, splitting. Assemble
   **Netherlands** and eyeball the network against the lab's output before matching exists.
   **Done (branch `network-completion`, 2026-10-05).** Acceptance harness:
   `tools/NetworkLab> dotnet run -- port-check <regionId>` runs the app's real
   `RegionNetworkAssemblyService` against the dev database from the lab's cached fetches and
   prints the persisted rows; passenger/bonus km must equal the lab's to 0.1 km. The route-types
   PUT copies fields explicitly, so the current UI cannot reset `MatchesRailNetworks`; it must
   not be added to that PUT until the checkbox exists, or the old frontend would post it false.
2. Matching service and queue hooks; rematch NL; compare numbers with the lab (99.6 % rail,
   93.7 % urban at 20 m — expect ~equal at 15 m).
3. Read API, stats page with partial rendering, missing list.
4. Curation UI and overrides; admin assembly actions.
5. Provinces and neighbouring countries on demand; per-sub-region fetch when a large country is
   wanted; a Swiss region to confirm rack railways.

Not in v1: periodic automatic reassembly, a completion cache table, funicular/monorail/ferry
modes, country-level assembly with region spans (Appendix B).

## 11. Evidence: the NetworkLab validation (Aug–Sep 2026)

`tools/NetworkLab` is a standalone console app (reads the dev database, caches each Overpass
fetch once, exports GeoJSON for `viewer.html`). It ran the full pipeline on the Netherlands
(region 1) and North Rhine-Westphalia (region 5) with the user's real routes. Final numbers:

| Network | Passenger route-km | Covered @20 m | Bonus ridden |
|---|---|---|---|
| NL Rail | 2,762 | **99.6 %** (missing list empty) | 152 km |
| NL UrbanRail | 487 | 93.7 % (94.0 % fractional) | 23 km |
| NRW Rail | 3,558 | **97.8 %** | 402 km |
| NRW UrbanRail | 764 | 74.8 % (75.9 % fractional) | 14 km |

What each design decision was learned from:

- **Union across rides (principle 5).** The VSM museum line, logged as three legs, and the
  STAR station at Stadskanaal, where one route arrived and another departed, both read ~0.53
  covered under per-route fractions although every metre was ridden. Per-sample union fixed
  both at even 10 m. Bitmaps on match rows follow from this.
- **Route relations (principle 2).** Switching the passenger flag from tags to relations took
  NRW rail from 83.1 % to 97.8 %: ~680 km of unserved track (WLE, Tecklenburger Nordbahn,
  Extertalbahn, Almetalbahn, Wiehltalbahn, draisines) left the denominator and the user's rides
  over them became bonus. The Rotterdam Beverwaard tram depot — 131 edges / 13.8 km of bare
  `railway=tram` with no tag or landuse to filter on, all falsely "covered" because depot
  spacing is inside the tolerance — fixed itself: depot tracks carry no relations. Caveat that
  motivates the override: museum lines are inconsistently mapped (Krefelder Schluff has a
  relation, the VSM does not).
- **Symmetric eligibility (principle 3).** Köln Stadtbahn line 7 to Benzelrath runs on
  `railway=rail` (ex-KFBE); with tram-typed rides restricted to UrbanRail it could never turn
  green. Relations then moved that track to UrbanRail anyway, but the symmetric rule stays —
  it is what makes the system robust to the next such case.
- **Tolerance (5.1).** Rail coverage barely moved between 10 and 30 m (wizard geometry is
  track-accurate); non-passenger bonus km grew 88 → 107.5 km (NL) and 207 → 261 km (NRW) with
  tolerance — parallel freight tracks, i.e. false positives. After twin probes made real
  coverage tolerance-flat, 15 m became free.
- **Dedup (4.5).** Per-sample shadowing brought NL rail from 5,324 track-km to 2,762 route-km
  (ProRail: ~3,000; the shortfall is 4-track sections collapsing to one line, as wanted) and
  halved matching time. v1 kept one physical track and lost hidden-side rides beyond the
  tolerance (NL urban 90.7 % @10 m vs 92.2 % @30 m); averaging + twin probes made it flat
  (92.1–92.4 %). The user spotted single/double flicker SE of Utrecht; the "interior visible
  run with shadow on both sides" rule removed it (gray zone → 0 edges) and 44 km of
  double-counted corridor fragments.
- **Mid-edge termini (principles 1, 4.6).** Measured cost of binary edges on in-progress
  regions: NRW rail had 36 km ridden-but-uncounted across 51 partial edges (~0.85 pp), NRW
  urban 13.8 km; Köln's Nord-Süd-Stadtbahn sat at 0.73 and appeared in the missing list though
  mostly ridden. Station splitting recovered ~11 km, fractional credit ~22 km more. That
  experience — three patches to make binary edges honest — is why v2 drops the binary edge.
- **Modes.** Four modes (Rail/LightRail/Metro/Tram) became two: the distinction is hopeless in
  OSM tagging and in user perception (Ruhrgebiet Stadtbahn is all three), and it fragmented
  coverage. Urban edges split at OSM stops, not DB stations (which exclude tram/metro).
- **Rejected alternatives.** Buffered-polygon coverage (Trainlog's model: area-weighted,
  first-touch marks a blob), hex-grid coverage, and station-pair graphs lost to the geometric
  network at the outset; junction-projection conflation and buffer/skeleton centerlines lost
  to per-sample shadowing for double track.

Pre-existing data quirk seen along the way: the stored Netherlands region polygon (and the
Overpass area) includes the Caribbean — an Aruba tourist trolley landed in the NL urban network.

### 11.1 v2 conformance run (2026-10-05)

The lab was switched to the v2 dialect — uniform chainage lattice for matching, the 4-sample
storage floor, interval export, partial rendering in the viewer — and rerun on both regions
from the cached fetches. This is the reference output the port must reproduce:

| Network | Route-km | @15 m (default) | @20 m | vs. §11 @20 m |
|---|---|---|---|---|
| NL Rail | 2,762.1 | **99.6 %** (2,749.9 km) | 99.6 % | identical |
| NL UrbanRail | 486.9 | **93.9 %** (457.0 km) | 93.9 % | −0.1 pp |
| NRW Rail | 3,558.3 | **97.9 %** (3,482.1 km) | 97.9 % | identical |
| NRW UrbanRail | 764.1 | **75.3 %** (575.4 km) | 75.5 % | −0.4 pp |

Percentages are the exact (fractional) figure. Lattice equivalence holds: the per-segment and
chainage samplers agree to ≤ 0.1 pp on rail; the urban drift comes from the storage floor —
4 samples (~100 m) is stricter than the lab's earlier 5 %-of-edge floor on the short
stop-split urban edges, and is the intended behaviour. NL matched 2,735 eligible routes in
8 s, NRW 902 in 8 s.

**Simplified-geometry gate: failed, and fixed differently than the doc assumed.** Overall
simplified/full length is 99.6–99.9 %, but the worst edges ≥ 500 m sit at 94–98 %: a curvy
half of an edge loses more length than a straight half, so a fraction of *full* length lands
visibly off when applied to the *simplified* polyline. Instead of tightening the simplifier,
intervals are **re-expressed in the simplified geometry's own chainage**, mapped through the
vertices Douglas-Peucker keeps (it preserves original vertex coordinates exactly, so the
piecewise-linear mapping between the two chainages is lossless). The completion endpoint does
this before emitting intervals; the "within ~1 %" constraint on `SimplifiedGeometry` is gone.

**Port acceptance (same day):** the app's `RegionNetworkAssemblyService`, fed from the lab's
cached fetches via `port-check`, persisted NL Rail 2,762.1 / 361.7 km and UrbanRail
486.9 / 56.2 km (passenger / non-passenger) — identical to the lab — with 745 + 842 edges
carrying hidden twins, 755 + 873 station labels and a maximum edge of 9.99 km. Dedup, twin
attachment, splitting and the cap all survived the move.

## Appendix A — decisions

- Percentage over 100 % was considered and rejected; bonus km instead.
- Two modes, not four. Trains, trams and metros all cover both.
- `TrainlogType` is not an OVDB concept and does not drive matching; `MatchesRailNetworks` does.
- Partial rendering is a core requirement, not a follow-up.
- Museum lines without route relations are bonus, not passenger network; fix per case with an
  override or in OSM.
- Per-region assembly with duplication across the hierarchy, for now.

## Appendix B — considered, deferred: country-level assembly with region spans

Assemble once per top-level country and store `(edge, region, sampleFrom, sampleTo)` spans
for sub-regions; completion per province = bitmaps AND span masks. Removes duplicate storage
and duplicate matching, but moves clipping semantics to read time and adds a table. The lab
assembled NRW directly without trouble; revisit only if duplication actually bites.
