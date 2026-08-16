# Plan: station visit history

Give every station visit a date, and keep it easy enough that dating them is not a chore.

Figures marked **(measured)** come from a restore of the production dump into the local dev
database (`dev-db/README.md`). Everything else is a design decision.

## Requirements

1. **Station visits should be dated.**
2. **Backfill dates existing visits from geometry-close route instances.**
3. **Importing a route may suggest stations** that were not marked — ones the trip stopped at, or
   started/ended at.
4. **Marking a station tries to match it** to a route instance, or to a Träwelling trip that has
   not been imported yet.
5. **Two levels are recorded: *stopped at* and *got on/off*.** Captured from the start, because the
   backfill is a human pass over ~5,739 stations and nobody is doing that twice.

Plus one absolute constraint:

**Never automatically mark a station as visited.** Every `StationVisit` originates in an explicit
user action. No import, webhook, migration or backfill job may create one — inference may only ever
*propose*. Enforce it structurally: creation goes through a single service method that requires a
user-action source, with a test asserting no other path writes to the table. Stated this strongly so
it survives someone later adding a well-meaning import path.

## What the data says (measured)

- 9,260 visits; 5,901 for the primary user (5,739 on active stations).
- 26,391 active stations; 24,437 route instances, 4,882 Träwelling-linked (~20%).
- All 12,809 routes have `LineString` geometry.
- **50 of 50** sampled *visited* stations lie within ~300 m of a ridden route → a dating candidate
  exists for essentially every visit. Backfill by geometry is viable.
- **14 of 100** sampled *unvisited* stations also lie within ~300 m of a ridden route → ~2,900
  stations passed but deliberately not marked. Proximity alone is ~66% precise, which is why it may
  propose but never decide.

## Model

Only `StationVisit` changes. No candidate table, no calling-pattern table.

```csharp
// Two levels, as two dates. Getting on or off implies the train stopped, so
// FirstStoppedDate <= FirstEntryExitDate whenever both are known.
public DateTime? FirstStoppedDate { get; set; }         // local civil date at midnight
public DateTime? FirstEntryExitDate { get; set; }       // local civil date at midnight

public int? FirstStoppedRouteInstanceId { get; set; }   // FK, ON DELETE SET NULL
public int? FirstEntryExitRouteInstanceId { get; set; } // FK, ON DELETE SET NULL

public StationVisitSource Source { get; set; }          // Legacy=0, Web, Telegram, ImportSuggested, Backfill
public DateTime? CreatedOn { get; set; }                // UTC; null = predates this feature
public DateTime? UnvisitedOn { get; set; }              // UTC; tombstone
public bool DatingSkipped { get; set; }                 // "stop offering me dates for this one"
```

**The level is derived from which dates are present**, so there is no enum to keep consistent with
them:

| `FirstStoppedDate` | `FirstEntryExitDate` | Level |
|---|---|---|
| null | null | Visited, not yet dated (all 9,260 legacy rows start here) |
| set | null | Stopped at |
| set or null | set | Got on/off |

"Visited at all" remains "a row exists", so the global query filter and the nine existing read paths
are unaffected by the split.

- `DateTime` at midnight, matching `RouteInstance.Date`; **local civil date**, converted via
  `ITimezoneService` at the station's coordinates, so a day-granular statistic never shifts at
  midnight.
- `Source = Legacy (0)` labels all 9,260 existing rows correctly for free.
- No `VisitCount`, no `LastVisitedOn`: toggles measure taps, not visits, and pre-feature history is
  unrecoverable, so a count could never be honest.
- Indexes `(UserId, FirstStoppedDate)` and `(UserId, FirstEntryExitDate)`.
- **Global query filter** `HasQueryFilter(sv => sv.UnvisitedOn == null)`. This is what makes the
  tombstone safe: all nine existing is-visited predicates keep working unchanged, including
  navigation subqueries, and future queries cannot forget it. The few places that must see
  tombstones opt out with `IgnoreQueryFilters()`.

### The tombstone does double duty

A row with `UnvisitedOn` set means "the user said no". If it has a date, it is a visit that was
removed and the date is preserved for a possible return. If it never had one, it is a **declined
suggestion** — which is exactly what stops requirement 3 from re-proposing the same station after
every import. One concept, two jobs, no extra table.

### Migration

Six nullable/defaulted `ADD COLUMN`s on a 9,260-row table. Instant on MariaDB 11.4, nothing
destructive. Hand-check the scaffold for the two traps this repo has hit before:

- `CreatedOn` must stay **nullable** — null honestly means "predates the feature". Do not let EF
  emit `NOT NULL` with a backfilled default.
- The trip FK must be **`ON DELETE SET NULL`**, not `CASCADE`, or deleting a trip deletes visits.

Rehearse on the dev dump before it auto-applies on startup.

## The one primitive

Everything below is the same matcher, called in one of two directions:

- **station → trips**: which of my route instances plausibly explain being at station S, earliest
  first?
- **trip → stations**: which stations does this route instance plausibly explain?

Each match carries **why**, and the why is ranked — note the order is not the obvious one:

| Evidence | Means | Strength |
|---|---|---|
| Route endpoint (`From`/`To` matches the station) | You started or ended a journey here, so you stood on the platform | Strongest |
| Träwelling stopover | The train *called* here; does not prove you got out | Strong |
| Geometry within ~300 m of the line | The line passes here; cannot distinguish stopping from passing | Weakest |

Computed **on demand**, never stored:

- *trip → stations* has to be live anyway, because the trip was just imported.
- *station → trips* is fast enough to run per station during review.

Implementation: an in-memory STRtree of prepared route geometries, the pattern already used by
`StationRegionsService`, built once and cached. Build it from lightly simplified geometry (finer
than the ~200 m replay tolerance, or the 300 m threshold becomes meaningless). **Do not** take a
dependency on the manual spatial indexes in `docs/spatial-indexes.md`.

Träwelling stopovers come from `GET /stopovers/{ids}`, batched by trip id — the check-in already
carries `TrawellingTransport.Trip` — fetched during the existing sweep and used transiently. They
are not persisted: their only jobs happen at import, and afterwards nothing reads them.

## Marking a station (requirements 1 and 4)

A mark should be born dated wherever possible, rather than dated later by a chore.

**Manual marking defaults to "got on/off", and does not ask.** If you are standing at a station, or
picking one deliberately off a map, you almost certainly got on or off it. "Stopped at" arrives
overwhelmingly from import suggestions and backfill, so the quick paths must not be cluttered with a
question whose answer is nearly always the same. Correction is one tap away when it is wrong.

- **Telegram, at the station.** Tapping a station sets `FirstEntryExitDate` (and
  `FirstStoppedDate`) to today in the station's timezone — the one surface where "now" is truth. The
  confirmation message carries **Undo** and **Only stopped** buttons, so the rare correction costs a
  second tap and the common case costs none.
- **Web map.** Clicking a marker marks it as got on/off, as now — but with no implicit date, since
  this is how you retro-mark from the sofa. The matcher runs immediately and the snackbar *offers*
  the answer — "Marked Zwolle. Got on/off 3 May 2024 on Utrecht → Groningen?" — with **Only
  stopped**, **Pick a date** and **Undo** beside it. An undated visit is still valid; it simply joins
  the backfill queue.
- **Station popup** is the full-control surface: both dates with their trips, each editable or
  clearable. The quick paths stay quick; this is where anything unusual gets fixed.
- **Marked before the trip exists.** The usual case: you tap at the platform, the Träwelling
  check-in imports later. No pending-status column is needed — when an instance is created or
  imported, the matcher runs over the user's unlinked visits from ±1 day and links what it
  explains. Requirement 4's "or a Träwelling trip" is satisfied by deferral, not by extra state.

Links are always opportunistic and silent. Never ask the user about a link: a null link with a
correct date beats a guessed link.

## Importing a route (requirement 3)

After an import, run *trip → stations* and show what the trip explains but is not marked, grouped by
evidence:

- **Started or ended here** — strongest, listed first.
- **The train stopped here** — from the calling pattern, when there is one.
- **Passed nearby** — proximity only, listed last and visually distinct.

Every one is off by default. Ticking one **marks the station** (the explicit user action the base
requirement demands) and dates it from that trip **at the level its group implies** — the first two
groups set entry/exit and stopped respectively, and a proximity tick asks which. Leaving it alone
does nothing. Explicitly dismissing one writes an undated tombstone, so it is not offered again on
the next trip through.

This is the only flow that creates visits from suggestions, and it is opt-in per station.

## Backfill (requirement 2)

**Dating only. It never creates a visit**, so this path is structurally incapable of violating the
base requirement, and the ~2,900 passed-but-unmarked stations do not appear in it at all.

Work the queue of visits with no level yet — both dates null — and `DatingSkipped` false. For each,
run *station → trips*:

- A **map showing the station with the selected trip's route drawn on it**; changing selection
  redraws. Seeing the line sweep through the station is the evidence — a lone pin is not.
- **Candidate trips oldest first, the oldest selected by default**, each showing its date, route
  name and the evidence tier. The default is usually right; where it is not, the earlier trips are
  visible and one tap away.
- Three outcomes, none of which can un-mark anything: **Got on/off here** sets
  `FirstEntryExitDate` (and `FirstStoppedDate` to the same date, since alighting implies stopping);
  **Only stopped here** sets `FirstStoppedDate`; **Not this trip** sets `DatingSkipped` and moves
  on. The first two are the same single decision split two ways, so recording the level costs no
  extra taps.
- There is deliberately no "deny" — denial would assert "I have not visited this", which is not a
  question this flow asks. Un-marking lives on the map and in Telegram, behind the dialog.
- Confirming **auto-advances**.

**No bulk confirm, anywhere.** One station, one tap. At a generous four seconds each, 5,901 visits
is a few evenings and it happens once. The speed has to come from **defaults being right**, not from
operations that decide many stations at once — and dropping bulk confirm also removes any need for
batch undo.

Endpoint evidence is scarcer than it looks (**measured**): of this user's 5,901 visited stations,
1,220 (21%) match a route's `From`/`To` by exact name, and a 300-station sample puts geometric
endpoint matching at 27%. Even combined, roughly a third. So most of the queue rests on proximity
plus the user's memory, which makes the defaults matter more, not less.

The defaults that keep it moving:

- **Trip: the oldest candidate, pre-selected.** Where only one candidate exists, there is nothing to
  choose.
- **Level: "got on/off", pre-selected.** Every legacy row was marked under a single-level regime
  where "visited" colloquially meant *I have been here*, so entry/exit is the better prior — and for
  endpoint matches it is true by definition.
- **Order the queue by candidate trip**, so consecutive stations share a journey. Confirming eight
  stations from the same Utrecht → Groningen run in a row costs far less thought than eight
  unrelated ones. This is ordering, not bulk: still one tap each.
- **Show the distance** on proximity candidates, so an implausible 290 m match in a dense tram area
  is visible rather than assumed.

So the per-station interaction is: glance at the map, tap **Got on/off** (or **Only stopped**, or
**Skip**), advance.

Re-running later is free and worth doing: new trips can date stations that had no candidate before.
`DatingSkipped` is what stops it nagging about the genuinely undatable.

## Un-marking

The invariant: un-marking never silently destroys a date the user established, and never silently
preserves one they created by accident. Ask rather than infer from elapsed time.

- **Row has a date or a trip link:** dialog — *"I tapped this by mistake"* (delete the row
  outright) or *"I haven't visited this"* (tombstone, keep the date). Telegram has no modals; use an
  inline keyboard replacing the message.
- **Row is dateless and unlinked:** delete silently. Nothing to lose, and this is the common
  mis-tap, so friction tracks risk.
- **Re-visiting a tombstone:** clear `UnvisitedOn`, keep the date, and *show it* — "first visit
  3 May 2024" — so a stale restored date is seen rather than silent.
- **Edit date** from the station popup is the correction path, not un-mark/re-mark.
- **No batch undo is needed**, because there is no bulk confirm: every backfill decision is a single
  station, correctable from its popup like any other.
- **Irreversible:** the explicit "by mistake" delete, and station merge.

### Station merge

`StationMergeController.MergeStations` currently deletes the duplicate visit row outright. Once rows
carry dates that can destroy the *earlier* one. It must keep the earliest non-null value of **each**
date independently, with its matching trip link — the surviving row may take its stopped date from
one duplicate and its entry/exit date from the other.

## Why both levels, from the start

A train calling somewhere is not the same as getting out, and the two are worth collecting
separately. The argument for capturing it **now** rather than later is sequencing, not schema:

- The backfill is a manual pass over ~5,739 stations. Capturing only "visited + date" now and
  deciding later that entry/exit matters would mean **doing that pass a second time**. Schema
  changes are cheap and reversible; a human re-review of 5,739 stations is neither.
- During the review the classification is largely free: an endpoint match (`From`/`To`) *is*
  entry/exit, so the endpoint pass needs no extra input at all.
- It costs no extra taps elsewhere either — one Confirm button becomes two.

The earlier objection — that "stopped at" is only knowable for Träwelling trips, forward-only — is
weaker than it looked, because asking at import turns transient evidence into a durable **user
assertion**. The sparseness of the source stops mattering once the answer is recorded.

The remaining honest limitation: legacy rows have no level until they are reviewed, so the UI needs a
"visited, level unknown" state during the transition. That is the same honest-unknown the rest of
this plan uses, not a defect.

## Queries that must change

The global query filter means the nine read paths change **zero lines** — that is the point. What
actually changes: `StationController.UpdateVisitedStations` and
`TelegramBotService.HandleCallbackQueryAsync` (new semantics; `IgnoreQueryFilters()` to find
tombstones), `StationMergeController` list and `MergeStations` (tombstone-aware, earliest-date
merge), and the two admin counts in `StationController.GetAdminMap` and `StationMergeController`
(decide active-only explicitly rather than discovering it).

Verify the rest by test rather than by reading: one integration test over
mark → un-mark → re-mark asserting the map payload, region percentages and missing-stations all
agree.

New consumers: `GetYearInReview` gains `NewStations` plus an honest `UndatedVisitedStations`, so the
figure is never presented as complete while dating is partial. `AchievementService` progressions are
`(Date, Value)` pairs, so undated visits cannot participate — station achievements count dated
visits only, and say so.

**The two levels surface as two figures, not as an ambiguous one.** Region completion shows two
percentages, stopped and entry/exit, and the region overview colours each station:

| Colour | Meaning |
|---|---|
| Blue | Got on/off here |
| Green | Stopped here |
| Grey | Visited, level not yet known (legacy, pending review) |
| Red | Nothing |

Grey disappears as the backfill progresses. Everywhere that only needs "visited at all" — the
admin counts, the Telegram percentage, missing-stations — keeps using row existence and is
unaffected.

## Rollout

1. **Migration, tombstone semantics, un-mark dialog.** Columns, global query filter,
   `IgnoreQueryFilters` in the four spots, merge fix, toggle-cycle test. No visible change except
   that toggles stop destroying data.
2. **The matcher.** One service, both directions, cached STRtree. Nothing user-facing yet; testable
   in isolation.
3. **Dating at the edges.** Telegram stamps today; the web snackbar offers the matched date;
   the station popup shows date, source and trip, with an edit dialog.
4. **Backfill.** Endpoint pass with bulk confirm, then a proximity skim.
5. **Import suggestions.** The post-import list, off by default, dismissals as tombstones.
6. **Consumers.** "New stations this year", discovery timeline, station achievements.

## Failure modes

**Silent, therefore dangerous:**

- EF scaffolding `CASCADE` on the trip FK → deleting a trip deletes visits. Hand-check; test.
- Timezone off-by-one when stamping near midnight; a day-granular stat never looks wrong enough to
  notice. Convert at the station's coordinates; test a UTC+13 case.
- Over-simplified geometry in the STRtree quietly widening the 300 m threshold.
- A stale date restored on re-visiting a tombstone. Mitigated by always displaying it.
- The global filter quietly changing admin or merge behaviour. Decide and test both explicitly.
- Backfill dating a visit to an earlier *passing* trip. Mitigated by showing trips oldest-first with
  their evidence tier, and by proximity never being bulk-confirmable.
- **An accidental mark never noticed.** Accepted deliberately: it can be dated wrongly but not
  invented, and it is fixable on sight.

**Loud, therefore acceptable:** a failed startup migration (rehearsed on dev-db first) and rate
limiter exhaustion from stopover fetching (already surfaced by the existing budget).

## Not building

A candidate table (matches are computed on demand, in both directions, by one service); a
calling-pattern table (used transiently at import, then discarded); a level enum (the level is
derived from which dates are set); `VisitCount`; `LastVisitedOn`; an approximate-date tier (a date is
user-asserted, trip-derived, or null); any auto-marking; a resurrected Träwelling station mapping
table; Träwelling-API-driven backfill; event-sourced visit state; database spatial indexes for this
feature.

## Still open

- **The proximity threshold is fine at ~300 m, for now** (**measured**, and it contradicted the
  expectation): in a 400-station sample of this user's visits, 374 have no other active station
  within ~300 m, 22 have one, and 4 have two — unambiguous 93.5% of the time. The feared tram and
  metro density does not appear, most likely because `StationImporterController` imports
  `railway=station|halt` and therefore **excludes `tram_stop`** entirely. One number is enough
  today; revisit the moment tram or metro stops are imported as stations, because that assumption is
  doing the work, not the geometry.
- **The two dates need an ordering rule when edited.** Alighting implies stopping, so setting
  `FirstEntryExitDate` earlier than `FirstStoppedDate` is incoherent. Pull the stopped date back to
  match, or refuse — decide before the edit dialog is built.
- **`DatingSkipped` can go stale.** A visit skipped because nothing plausible was on offer should
  probably resurface when a newly imported trip provides a candidate; a visit skipped because the
  user genuinely cannot remember should not. Same flag, two meanings — either split it or accept the
  nagging.
- **Import suggestions need a volume guard.** A forty-stop journey should not present forty
  proposals; collapse the proximity group behind a count by default.
- Whether station achievements wait for backfill or ship counting dated visits only.
- How aggressively to simplify geometry in the STRtree — a measurement, not a decision: check the
  memory footprint of the prepared geometries before choosing.
