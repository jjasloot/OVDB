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

Measured again once the matcher existed, over 300 real undated visits:

- **294 of 300 (98%) have at least one candidate trip.** Backfill by geometry is not merely viable,
  it covers nearly the whole queue.
- **148 of 300 (49%) have at least one endpoint-evidence candidate** — better than the 21%/27%
  single-signal figures below, because the union of name and geometry catches what either misses.
- **60.8 candidate trips per station on average, across 26.2 distinct routes.** The candidate list
  is far too long to show raw; see the preselection rule in Backfill.
- **The oldest candidate is endpoint-grade only 15% of the time.** This is the finding that changed
  the design: pre-selecting the oldest trip outright would usually pre-select something that merely
  passed through.
- Index cost: 4.5 s cold build, 98 MB peak, 69 MB steady, and ~6 ms per station warm.

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

**Marking defaults to "stopped at". Entry/exit costs one more tap.** Stopped is the weaker claim, so
the default can never over-claim: upgrading to entry/exit is always a deliberate act, and a
mis-tap records the least it could mean rather than the most. Same principle as preferring an honest
unknown to a plausible guess.

- **Web map.** Tapping a marker marks it visited (stopped at). A snackbar **with no expiry** offers
  **Undo** and **Entry/exit**, so the correction is available for as long as it takes to notice —
  not for five seconds. Tapping an already-visited marker opens a small popup: remove it, or toggle
  between stopped and entry/exit.
- **Telegram.** Tapping an unvisited station marks it stopped at, and the confirmation message
  carries an **Entry/exit** button. Tapping an already-visited station replies with its current
  state and the options — upgrade, downgrade, remove.

**Dating is a web concern only.** The two surfaces are asking different questions, so they get
different answers:

- **Telegram is just-in-time.** You are standing on the platform. Today, in the station's own
  timezone, is not a guess — it is the answer. No date UI belongs there, and none is offered.
- **The web is retrospective.** Tapping a marker on a map at home says nothing about when, so it
  must not stamp today; that would be a fabricated date, and a wrong date is worse than none. The
  web therefore offers, never assumes: the visit is born undated and the user can **pick a date** or
  **attach a trip** — the matcher's candidates, which date it as a side effect — whenever they know
  it. Undated stays a first-class state.

So: **Station popup** is the full-control surface, and the only one. Both dates with their trips,
each settable, changeable or clearable, either by picking a date directly or by choosing from the
matcher's candidate trips. The quick paths stay quick; anything unusual is fixed here.

**Editing obeys one ordering rule: alighting implies stopping, so the stopped date is pulled back to
match whenever the entry/exit date is set earlier.** Never refuse the edit. The user asserting they
got off on a date is also asserting the train stopped then, and the same invariant is already what
`MarkAsync` enforces when entry/exit fills both levels — an edit dialog that argued instead would be
enforcing a rule the rest of the system does not. Note this is the one path allowed to move a date
**later**: `MarkAsync` only ever moves them earlier, so a correction needs its own entry point
rather than an extra flag on that one.
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

- **Trip: the oldest _endpoint-grade_ candidate, pre-selected; the oldest of any grade only when
  there is no endpoint match at all.** The original rule was simply "oldest", but the matcher's own
  numbers killed it: the oldest candidate is endpoint-grade only 15% of the time, so "oldest" would
  usually pre-select a train that passed through without stopping and date the visit too early. An
  endpoint match exists for 49% of the queue, and where it does it is both strong and specific.
  Where it does not, the oldest proximity candidate is pre-selected and **labelled as the weak
  guess it is**.
- **Collapse the list to one row per route, earliest instance first.** 60.8 candidates across 26.2
  routes is not a list anyone skims; per route, only the earliest instance can be the answer to
  "when did I first come here". Other instances stay reachable behind the row, not in front of it.
- **Level: "stopped at", pre-selected** — except on endpoint matches, where entry/exit is true by
  definition and is pre-selected instead. Defaulting to the weaker claim means a fast tap-through
  never invents an alighting that did not happen.
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

Legacy rows need no third state. **Every visit is at least a stop**, so a row with no dates on it is
"stopped at", not "level unknown" — the level derives from `FirstEntryExitDate` alone, and the
9,260 rows that predate visit history are simply stopped-at visits that are not yet dated. Whether a
visit is dated is a separate question from what it means, and only the first of those is what the
backfill answers.

## Queries that must change

The nine read paths that only ask "visited at all" change **zero lines** — row existence still
answers that. What actually changes: `StationController.UpdateVisitedStations` and
`TelegramBotService.HandleCallbackQueryAsync` (both go through `StationVisitService`),
`StationMergeController.MergeStations` (earliest-date merge rather than delete-the-duplicate), and
the two map projections that now also report the level.

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
| Green | Stopped here (dated or not) |
| Red | Nothing |

Three colours, not four: an undated visit is green like any other stop, because that is what it is.
Everywhere that only needs "visited at all" — the admin counts, the Telegram percentage,
missing-stations — keeps using row existence and is unaffected.

## Rollout

1. **Migration and the write boundary.** Columns, indexes, `StationVisitService` as the only writer,
   merge fix, architecture test. No visible change.
2. **Marking at two levels.** Web map three colours, non-expiring snackbar offering the entry/exit
   upgrade, un-mark dialog; Telegram verbs on the same service.
3. **The matcher.** One service, both directions, cached STRtree. Nothing user-facing yet; testable
   in isolation.
4. **Dating on the web.** Telegram already stamps today and needs nothing further. The web gets the
   date controls: the snackbar offers a matched date when the matcher found one, and the station
   popup gains an edit dialog — pick a date, or pick a candidate trip and take its date.
5. **Backfill.** One station at a time — map, candidate trips oldest-first with the oldest
   preselected, confirm or deny. No bulk confirm.
6. **Import suggestions.** The post-import list, off by default, dismissals recorded separately.
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
- **`DatingSkipped` can go stale.** A visit skipped because nothing plausible was on offer should
  probably resurface when a newly imported trip provides a candidate; a visit skipped because the
  user genuinely cannot remember should not. Same flag, two meanings — either split it or accept the
  nagging.
- **Import suggestions need a volume guard.** A forty-stop journey should not present forty
  proposals; collapse the proximity group behind a count by default.
- Whether station achievements wait for backfill or ship counting dated visits only.

**Settled by measurement** (was: how aggressively to simplify the geometry): **50 m Douglas-Peucker,
indexed as segments rather than whole routes.** Whole-route envelopes are useless as an index — a
cross-country line's bounding box covers half the country's stations — so the tree holds 476k
segments. Kept whole, the geometry is 9.9M coordinates and 382 MB; at 50 m it is 65 MB and changes
what the 300 m threshold finds by about 1%, which is noise against the weakest evidence tier. 25 m
was also measured: 94 MB for no useful gain. The index is streamed into place rather than loaded and
then simplified, which holds the peak to 98 MB instead of roughly six times that, and it is dropped
after 30 idle minutes because the work is bursty.
