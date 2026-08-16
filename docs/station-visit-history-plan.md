# Plan: station visit history

## Goal

Know *when* a station was first visited, not just *that* it was — reliably enough to build
statistics on, and forgiving enough that a mis-tap never destroys history.

## Current state

`StationVisit` (`OVDB_database/Models/StationVisit.cs`) is a bare join row: `Id`, `StationId`,
`UserId`, with a unique index on `(StationId, UserId)`. There is no timestamp, and both toggle
paths **hard delete** the row when un-visiting:

- `StationController.UpdateVisitedStations` (`OV_DB/Controllers/StationController.cs:81`) —
  `DbContext.Remove(stationVisit)`.
- `TelegramBotService.HandleCallbackQueryAsync` (`OV_DB/Services/TelegramBotService.cs:120`) —
  same, driven by an inline keyboard after a location share.

Consequences today: an accidental toggle is unrecoverable, re-visiting is indistinguishable from
a first visit, and anything time-based (see "What this unlocks") is simply not computable. This is
why the year-in-review page has no "new stations this year" figure.

## Data model

**Phase 1 — extend `StationVisit` (recommended core).** No new table, no query rewrites:

| Column | Type | Meaning |
| --- | --- | --- |
| `FirstVisitedOn` | `DateTime?` (UTC) | First confirmed visit. Null = visited, date unknown (all existing rows). |
| `LastVisitedOn` | `DateTime?` (UTC) | Most recent visit. |
| `VisitCount` | `int` | Number of recorded visits, default 1. |
| `Source` | `enum` | `Manual`, `Telegram`, `TraewellingImport`, `Inferred`, `Backfill`. |
| `UnvisitedOn` | `DateTime?` (UTC) | Soft delete. Non-null = currently not visited. |

Store UTC; `ITimezoneService` already exists for display conversion.

Every existing "is visited" query must then filter `UnvisitedOn == null` — that is the one
breaking change, and it touches `StationController`, `StationMapsController`, `StatsController`
(`GetRegionStats`) and `TelegramBotService`. Worth doing in a single commit with a test.

**Phase 2 — optional `StationVisitEvent` log.** Append-only `(StationVisitId, OccurredOn, Kind,
Source)`. Only needed if you want true per-visit history ("I have been to Utrecht 47 times") or an
audit trail. Phase 1's `VisitCount` covers the headline number without it.

## Toggle semantics: surviving accidental taps

The rule that makes this safe: **un-visiting never erases `FirstVisitedOn`.**

1. **Soft delete.** Un-toggling sets `UnvisitedOn`; the row and its dates stay.
2. **Re-visiting restores.** Clearing `UnvisitedOn` keeps the *original* `FirstVisitedOn` rather
   than stamping today. A fat-finger round trip is therefore a no-op.
3. **Undo affordance.** The frontend already has a global snackbar — show
   "Marked Utrecht Centraal as visited · Undo" for a few seconds. Telegram gets an inline
   *Undo* button on its confirmation message.
4. **Idempotency.** Repeated identical toggles inside a short window do not bump `VisitCount`,
   so a double-tap does not read as two visits.
5. **Explicit correction over deletion.** Offer "edit first visit date" in the UI so fixing a
   wrong date does not require un-visiting and re-visiting.

## Telegram: marking before the trip is logged

This is the flow worth optimising, because it is the only one that happens *at the station*.

Today: share location → nearby stations → inline buttons toggle. With timestamps, the toggle
moment simply *is* the visit time — more accurate than any date derived from a trip logged later.

Additions worth making:
- Default to "now", with a follow-up button for "yesterday" / pick a date when marking
  retroactively from the sofa.
- Confirmation message shows the recorded date and an *Undo* button.
- Optional later reconciliation: when a Träwelling trip covering that time window is imported,
  link the visit to it (`RouteInstanceId` on `StationVisit`), giving "visited on this trip".

## Automatic visits from imports

Träwelling check-ins carry origin and destination stations with names and coordinates
(`stopover.station`), so imported trips could mark those stations visited *with an accurate
timestamp*, populating history going forward without any manual work.

**This is the riskiest part of the plan.** The Träwelling station cache table was dropped in the
August 2026 migration, so matching an upstream station to an OVDB `Station` has to be done afresh:
nearest station within a tight radius (~150 m) *and* a name similarity check. A false match silently
records a visit to the wrong station. Mitigations: only auto-mark above a confidence threshold,
always stamp `Source = TraewellingImport` so it can be audited or bulk-reverted, and keep the whole
behaviour opt-in in profile settings.

## Backfilling what already exists

- Existing rows keep `FirstVisitedOn = null`, displayed as "date unknown" rather than a guess.
- Optional one-off job: infer a date from the earliest `RouteInstance` whose route geometry passes
  within ~200 m of the station. The geometry and dates are already there, and
  `StationRegionsService`'s prepared-geometry/STRtree approach is the pattern to reuse. Anything
  derived this way is stamped `Source = Inferred` and rendered distinctly (e.g. "≈ 2019").

## What this unlocks

- **"New stations this year"** in year-in-review — currently impossible, and the one figure I had
  to leave out.
- Station discovery timeline ("stations found per year") and a station-discovery map replay.
- Region completion dates: "you completed Utrecht on 3 May 2024".
- Achievements keyed on firsts: 100th station, first station abroad, first of a new operator.
- Gap finder ordering by momentum ("regions you have been completing recently").

## Rollout

1. Migration adding the columns (all nullable / defaulted) — no behaviour change.
2. Switch both toggle paths to soft delete + timestamps; update every "is visited" query to
   respect `UnvisitedOn`. Test coverage for the toggle/untoggle/retoggle cycle.
3. Surface dates in the UI: station popups, station map lists, undo snackbar.
4. Telegram: date in the confirmation, Undo button, retroactive date option.
5. Optional: inference backfill job.
6. Optional: opt-in auto-visits from Träwelling imports.
7. Then the dependent features (new-stations-this-year, discovery timeline, achievements).

Phases 1–3 deliver most of the value and carry almost no risk; 5 and 6 are where the judgement
calls (and the possibility of wrong data) live.

## Open questions

- Should un-visiting stay possible at all, or should the UI only ever offer "correct the date"?
- Auto-visits from Träwelling: opt-in, opt-out, or not at all?
- Is `VisitCount` wanted, or is first-visit-only the real goal (which would drop Phase 2 entirely)?
- For inferred dates: show them, or keep them internal until confirmed?
