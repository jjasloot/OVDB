# Plan: station visit history

Knowing *when* a station was first visited. Currently impossible: `StationVisit` carries no
timestamp, and both toggle paths hard delete.

This supersedes the first draft of this document. Figures marked **(measured)** come from a
restore of the production dump into the local dev database (`dev-db/README.md`); everything else
is a design decision, flagged where it is a judgement call rather than a fact.

## Base requirement

**Never automatically mark a station as visited.** Every `StationVisit` originates in an explicit
user action. No import, webhook, migration, backfill job or reconciliation may create one.
Inference may only ever *propose*.

Enforce it as an invariant rather than a habit: creation of a `StationVisit` lives behind a single
service method that takes an explicit user-action source (`Web`, `Telegram`, `ImportConfirmed`,
`BackfillConfirmed` — all of which mean "a human clicked something"), with a test asserting no
other path writes to the table. The value of stating it this strongly is that it survives someone
later adding a well-meaning "helpful" import path.

## What the data says (measured)

- 9,260 station visits; 5,901 for the primary user (5,739 on active stations).
- 26,391 active stations; 24,437 route instances, of which 4,882 are Träwelling-linked (~20%).
- All 12,809 routes have `LineString` geometry.
- **50 of 50** sampled *visited* stations lie within ~300 m of a ridden route → geometry-based
  backfill has essentially complete coverage.
- **14 of 100** sampled *unvisited* stations also lie within ~300 m of a ridden route → roughly
  2,900 stations passed but deliberately not marked. Proximity has about **66% precision**.

That last figure is the empirical case for the base requirement: the user demonstrably
distinguishes stopping from passing, so anything that auto-marked from proximity would corrupt the
data at scale.

## Corrections to earlier assumptions

- **OSM stop members do not give exact-id matching.** `Stations.OsmId` holds
  `railway=station|halt` node ids; route relation members with role `stop`/`platform` are
  `public_transport=stop_position`/platform objects — different elements, different ids. OSM stops
  are also **transient**: `ImporterController.GetStops` returns them to the import UI and nothing
  persists them, and `Route` stores no OSM relation id. **OSM cannot power backfill**, only future
  imports.
- **Träwelling identity is better than feared.** The station cache table was dropped, but
  `TrawellingStation` still carries the upstream `id`/`uuid`. The cache is gone; the key is not.
- **Träwelling cannot power backfill either.** Inbox rows (and their payloads) are deleted on
  import, so historical stopovers would mean re-fetching ~4,882 statuses through the rate limiter.
- **Träwelling stopovers are cheap to fetch going forward.** `GET /stopovers/{ids}` takes
  comma-separated **trip ids** and returns stopovers keyed by trip id (auth optional). The
  check-in already carries the trip id — `TrawellingTransport.Trip` is mapped today — and the
  sweep pages statuses 15 at a time, so this is one extra call per page, not per status.
  (The spec's summary says "for statuses" while its parameter says trip IDs; the parameter is
  correct.)

## Decisions

1. **Never auto-mark** (above).
2. **The user always initiates.** No proactive prompting from check-ins. Live Träwelling data is
   used only to attach evidence to marks the user makes.
3. **Inference proposes, the user confirms.** One review queue, shared by live candidates and
   backfill.
4. **A dialog on unmark, not a time-based rule.** See below.
5. **Links are opportunistic and silent.** A null link with a correct date beats a guessed link.
6. **Stopovers are a filter, not a source.** They separate stopped-at from passed-through.

## Data model

### `StationVisits` — new columns

```csharp
public DateTime? FirstVisitDate { get; set; }              // local civil date at midnight; null = unknown
public int? FirstVisitRouteInstanceId { get; set; }        // FK, ON DELETE SET NULL
public RouteInstance FirstVisitRouteInstance { get; set; }
public StationVisitSource Source { get; set; }             // Legacy=0, Web, Telegram, ImportConfirmed, BackfillConfirmed
public DateTime? CreatedOn { get; set; }                   // UTC; null = predates this feature
public DateTime? UnvisitedOn { get; set; }                 // UTC; tombstone
```

- `DateTime` at midnight, not `DateOnly` — matches the existing `RouteInstance.Date` convention and
  avoids a new type mapping on the Microting Pomelo fork.
- **Local civil date, not UTC.** A day-granular statistic must not shift a day at midnight; convert
  through `ITimezoneService` at the station's coordinates (already used this way in
  `TrawellingService`).
- `Source = Legacy (0)` is the default, so all 9,260 existing rows are labelled correctly for free.
- No `VisitCount`, no `LastVisitedOn`: toggles measure taps, not visits, and pre-feature history is
  unrecoverable, so "I have been here 47 times" could never be honest.
- New index `(UserId, FirstVisitDate)`.
- **Global query filter** `HasQueryFilter(sv => sv.UnvisitedOn == null)`. This is what makes soft
  delete safe: all nine existing is-visited predicates keep working unchanged, including navigation
  subqueries, and future queries cannot forget it. The few places that must see tombstones opt out
  with `IgnoreQueryFilters()`.

### `StationVisitCandidates` — the review queue

```csharp
public enum CandidateSource { RouteEndpoint = 0, TraewellingStopover = 1, OsmStop = 2, Proximity = 3 }
public enum CandidateState  { Pending = 0, Confirmed = 1, Dismissed = 2 }

long Id; int UserId; int StationId; int? RouteInstanceId;   // SET NULL: evidence outlives the trip
DateTime ProposedDate; CandidateSource Source; CandidateState State;
string MatchedName; double? DistanceM; string UpstreamKey;  // evidence + match memory
Guid? BatchId;                                              // group confirm/dismiss for bulk undo
DateTime CreatedOn; DateTime? ResolvedOn;
```

Unique on `(UserId, StationId, RouteInstanceId)`, indexed on `(UserId, State)`. Dismissed rows are
**kept**, so re-running inference never re-proposes something already declined — the same reason
`TrawellingInboxStatus` keeps its dismissed state. Mirror that pattern rather than inventing a
second idiom.

Generate a candidate only when the station has no active visit (*new-station mode*) or has one with
`FirstVisitDate == null` (*dating mode*). A tombstone means the user said no: never re-propose.

### `RouteInstanceStop` — the calling pattern

*Judgement call: Fable ran out of budget before settling this.*

```csharp
long Id; int RouteInstanceId; int StationId; int SequenceNumber;
string UpstreamKey; string UpstreamName;   // Träwelling station uuid + name, for evidence
```

Per **instance**, not per route, because a specific run may skip stops. Rows are only written for
stopovers that matched an OVDB station; unmatched ones are skipped rather than stored with a null
`StationId` — simpler, at the cost of not being able to explain why a station is absent.

Deliberately **Träwelling only in v1**. The same table can later hold OSM route stop lists (which
would need a nullable `RouteInstanceId` for route-level patterns), which would close the gap that
currently rules OSM out for backfill — but that is a separate phase, not v1.

### Migration

Five nullable `ADD COLUMN`s plus one `int NOT NULL DEFAULT 0` on a 9,260-row table, and two
`CREATE TABLE`s. All instant on MariaDB 11.4; nothing destructive. Hand-check the scaffold for two
specific EF traps, both of which have bitten this repo before:

- `CreatedOn` must stay **nullable** — null honestly means "predates the feature". Do not let EF
  emit `NOT NULL` with a backfilled default.
- The trip FKs must be **`ON DELETE SET NULL`**, not EF's occasionally-scaffolded `CASCADE`, or
  deleting a trip deletes visits.

Rehearse on the dev dump before it auto-applies on startup.

## Toggle semantics

The invariant: **un-marking never silently destroys a date the user established, and never
silently preserves one they created by accident.** The dialog asks which case it is rather than
inferring it from elapsed time.

- **Mark (web).** Row created, `FirstVisitDate = null`, `Source = Web`. The web map is how you
  retro-mark from the sofa, so "today" would be a lie. Snackbar offers *Undo · Visited today? ·
  Pick a date*.
- **Mark (Telegram).** `FirstVisitDate = today` in the station's timezone — the one surface where
  "now" is truth.
- **Un-mark, when the row has a date or a trip link:** dialog — *"I tapped this by mistake"*
  (delete the row outright) or *"I haven't visited this"* (tombstone, keep the date). Telegram has
  no modals; use an inline keyboard replacing the message.
- **Un-mark, when the row is dateless and unlinked:** silently delete. Nothing to lose, and this is
  the common mis-tap, so friction tracks risk.
- **Re-visit a tombstone:** clear `UnvisitedOn`, keep the original date, and *show it* —
  "Visited — first visit 3 May 2024" — so a stale restored date is seen rather than silent.
- **Edit date** from the station popup is the correction path, not un-mark/re-mark.
- **Irreversible:** the explicit "by mistake" delete, and station merge. Everything else round-trips.
- **Bulk confirm/dismiss** is undone by `BatchId`, since a dialog per row would be punishing.

### Station merge

`StationMergeController.MergeStations` currently deletes the duplicate visit row outright. Once
rows carry dates that can destroy the *earlier* first-visit date. It must keep the earliest non-null
`FirstVisitDate` and its trip link, and reassign candidate rows with the same dedup.

## Linking a mark to a trip

Never ask the user. Link when unambiguous, leave null otherwise.

- **Telegram mark:** check only that day's instances for the user — a handful, no spatial index
  needed. If exactly one has the station in its calling pattern (or, lacking one, passes within
  ~300 m), link it.
- **Trip does not exist yet** — the usual case, since check-ins arrive before import. Re-run the
  same check when an instance is created or imported, over the user's unlinked visits from ±1 day.
- **Live check-ins** (webhook → inbox) give the strongest evidence, but a pending status is not yet
  a `RouteInstance`. Store the Träwelling status id in `UpstreamKey` as provisional evidence and
  resolve it to `RouteInstanceId` at import.
- **Web marks** have no time and get no link; they enter the queue in dating mode instead.

## Stopovers: a filter, not a source

Proximity says *the line passes here*; a calling pattern says *this train called here*. That
distinction is exactly what the 66% precision figure measures, and proximity cannot make it.

Two uses, neither of which surfaces the word "stopover" in the UI:

1. **Linking** becomes set membership — is this station in that trip's calling pattern — which also
   disambiguates two trips that both pass a station where only one stops.
2. **Candidate tiering** — called-at stations are proposable; merely-passed ones are not.

Fetch via `GET /stopovers/{ids}` batched per page during the existing sweep, budgeted through
`TrawellingService.SendAsync` like every other call.

## Candidate tiering

- **Propose** (eligible for bulk confirm): route endpoints with a name match within ~300 m;
  Träwelling stopovers matched within ~250 m with normalised-name agreement.
- **Suggest** (individual confirm only, never bulk): proximity within ~300 m of the line; anything
  250 m–1 km, or with a name mismatch. Rendered distinctly ("nearby — passed through?").
- **Always show the evidence**: matched OVDB name beside the upstream name, distance, trip, date.
  A fuzzy-identity error is only catchable at the moment someone is looking at it.
- **Match memory without a mapping table**: a confirmed candidate's `UpstreamKey` pre-ranks the
  same upstream station next time.

## Backfill

Same machinery, run over history.

- **Pass 1 — route endpoints** (high precision, no API calls, all 12,809 routes): match LineString
  endpoints and `From`/`To` names to stations, propose the earliest instance's date. These stations
  are nearly all already marked visited, so this is *dating mode* — confirming adds dates without
  changing any count. "Confirm all" is defensible here.
- **Pass 2 — proximity** (recall): stations within ~300 m of the line. Dating mode for the visited
  remainder; new-station suggestions (~2,900) off by default. Per-item only. Show the two or three
  earliest candidate trips, not one, because the earliest *passing* trip may predate the first real
  visit.
- One-shot admin-triggered job using the in-memory STRtree + prepared-geometry pattern already in
  `StationRegionsService`. **Do not** take a dependency on the manual spatial indexes in
  `docs/spatial-indexes.md`; a one-shot job does not earn them.

### Review UI

Two views over the same queue, because the two passes have very different shapes.

**Trip-centric**, for the endpoint pass: one trip, the stations it explains, confirm the lot.
Turns hundreds of decisions into a handful, and is only offered for propose-tier candidates.

**Station-centric**, for ambiguous proximity matches — one station at a time:

- A **map showing the station and the currently selected route drawn on it**. Switching selection
  redraws. Seeing the line sweep through the station is the evidence; a lone pin is not.
- The **candidate trips listed oldest first, the oldest selected by default**, each showing its
  date, route name, and distance from the line. The default is usually right and keeps the pace
  up, but it biases *early* — the earliest trip that passes a station may predate the first time
  the user actually alighted there — so the dates must be prominent enough that an implausible one
  is obvious.
- **Three outcomes, not two**, because "yes, but I have no idea which trip" is a common and honest
  answer:

  | | Dating mode (station already visited) | New-station mode (~2,900 passed-but-unmarked) |
  |---|---|---|
  | **Confirm** | Set `FirstVisitDate` from the selected trip and link it | Create the visit — this is the user action that satisfies the never-auto-mark invariant — and date it |
  | **Not this trip** | Leave undated, dismiss these candidates | *(n/a)* |
  | **Deny** | *(must not un-mark the station)* | Never been there; dismiss permanently |

  The two modes share a screen but not their consequences: denying a dating candidate must never
  remove an existing visit.

- Confirming **auto-advances** to the next station; confirming one candidate **resolves its
  siblings** for that station rather than leaving them pending; and a denial writes a dismissed
  candidate that stays reachable behind a "show dismissed" filter, so it is recoverable.

## Queries that must change

The global query filter means the nine read paths change **zero lines** — that is the point. What
actually changes: `StationController.UpdateVisitedStations` and
`TelegramBotService.HandleCallbackQueryAsync` (new semantics; must use `IgnoreQueryFilters()` to
find tombstones), `StationMergeController` list and `MergeStations` (tombstone-aware, earliest-date
merge), and the two admin counts in `StationController.GetAdminMap` /
`StationMergeController` (decide active-only explicitly rather than discovering it).

Verify the rest by test rather than by reading: one integration test over the
mark → unmark → re-mark cycle asserting the map payload, region percentages and missing-stations
all agree.

New consumers: `GetYearInReview` gains `NewStations` plus an honest `UndatedVisitedStations`, so the
figure is never presented as complete while backfill is partial. `AchievementService` progressions
are `(Date, Value)` pairs, so undated visits **cannot participate** — station achievements must
either count dated visits only and say so, or wait for backfill.

## Rollout

1. **Migration + soft delete + dialog.** Columns, both new tables, global query filter,
   `IgnoreQueryFilters` in the four spots, merge fix, toggle-cycle test. No visible change except
   that toggles stop destroying data.
2. **Dates at the edges.** Telegram stamps today with an inline-keyboard confirm; web snackbar with
   undo and "pick a date"; station popup shows date, source and trip, with an edit dialog.
3. **First consumers.** "New stations this year" with the undated caveat; discovery timeline.
4. **Candidates going forward.** Generation at trip save and Träwelling import (endpoints +
   origin/destination + stopovers); the review page; batch undo.
5. **Backfill.** Endpoint pass with bulk confirm, then a proximity skim.
6. **Later, if ever.** OSM stop persistence, region completion dates, station achievements.

## Failure modes

**Silent, therefore the dangerous ones:**

- EF scaffolding `CASCADE` on the trip FK → deleting a trip deletes visits. Hand-check; test.
- Timezone off-by-one when stamping near midnight — a day-granular stat never looks wrong enough to
  notice. Convert at the station's coordinates; test a UTC+13 case.
- A stale date restored when re-visiting a tombstone. Mitigated by always displaying it.
- A wrong fuzzy-identity confirm. Mitigated by evidence display and popup provenance.
- The global filter quietly changing admin or merge behaviour. Decide and test both, explicitly.
- Proximity backfill dating a visit to an earlier passing trip. Mitigated by offering several trips
  and never bulk-confirming proximity.
- **An accidental mark the user never notices.** Accepted deliberately: dateless, so it cannot
  corrupt any date-based statistic, and fixable on sight.

**Loud, therefore acceptable:** a failed startup migration (rehearsed on dev-db first), and rate
limiter exhaustion from stopover fetching (already surfaced by the existing budget).

## Not building

`VisitCount`; `LastVisitedOn`; an approximate-date display tier (with confirm-always, a date is
user-asserted, trip-derived, or null); any auto-marking; a resurrected Träwelling station mapping
table; Träwelling-API-driven backfill; event-sourced visit state; database spatial indexes for this
feature; full calling-pattern import for non-Träwelling trips in v1.

## Still open

- Whether `RouteInstanceStop` should carry unmatched stopovers (null `StationId`) so absences are
  explicable. Currently: no.
- Whether station achievements wait for backfill or ship counting dated visits only.
- Whether the "confirm all" bulk action should exist at all on first release, or only after the
  endpoint pass has been eyeballed once.
