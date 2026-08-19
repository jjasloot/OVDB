import { ChangeDetectionStrategy, Component, OnDestroy, OnInit, computed, inject, signal } from "@angular/core";
import { LatLng, LatLngBounds, Layer, Map as LeafletMap, circleMarker, polyline } from "leaflet";
import { LeafletModule } from "@bluehalo/ngx-leaflet";
import { MatButton } from "@angular/material/button";
import { MatProgressSpinner } from "@angular/material/progress-spinner";
import { MatProgressBar } from "@angular/material/progress-bar";
import { MatRadioButton, MatRadioGroup } from "@angular/material/radio";
import { FormsModule } from "@angular/forms";
import { DatePipe, DecimalPipe } from "@angular/common";
import { TranslateModule } from "@ngx-translate/core";
import { firstValueFrom } from "rxjs";
import { ApiService } from "src/app/services/api.service";
import { MapTileLayersService } from "src/app/services/map-tile-layers.service";
import { TranslationService } from "src/app/services/translation.service";
import {
  StationBackfillItem,
  StationVisitLevel,
  TripCandidateGroup,
} from "src/app/models/stationView.model";

/**
 * Dating the visits that have no date, one station at a time.
 *
 * It cannot mark a station visited: the queue is drawn from visits that already exist, and the only
 * outcomes are "when" or "I do not know". There is deliberately no deny button — this flow never
 * asks whether a station was visited, so it has no business un-marking one.
 *
 * No bulk confirm either. At a few seconds each the queue is a handful of evenings and it happens
 * once; the speed has to come from the default being right, not from deciding many at a time.
 */
@Component({
  selector: "app-station-backfill",
  templateUrl: "./station-backfill.component.html",
  styleUrl: "./station-backfill.component.scss",
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [
    LeafletModule,
    MatButton,
    MatProgressSpinner,
    MatProgressBar,
    MatRadioGroup,
    MatRadioButton,
    FormsModule,
    DatePipe,
    DecimalPipe,
    TranslateModule,
  ],
})
export class StationBackfillComponent implements OnInit, OnDestroy {
  private apiService = inject(ApiService);
  private mapTileLayersService = inject(MapTileLayersService);
  private translationService = inject(TranslationService);

  private baseLayers = this.mapTileLayersService.createBaseLayers();
  options = {
    layers: [this.mapTileLayersService.defaultLayer(this.baseLayers)],
    zoom: 11,
  };
  leafletLayersControl = { baseLayers: this.baseLayers, overlays: {} };

  item = signal<StationBackfillItem | null>(null);
  loading = signal(true);
  saving = signal(false);
  selected = signal<number | null>(null);
  layers = signal<Layer[]>([]);
  // Never null: the Leaflet directive wants real bounds, and the country box is a sane opening view
  // for the moment before the first station loads.
  bounds = signal<LatLngBounds>(
    new LatLngBounds(new LatLng(50.656245, 2.92136), new LatLng(53.604563, 7.428211))
  );
  /** Stations looked at and left alone this session; nothing is recorded about them. */
  private passed = signal(0);
  private done = signal(0);

  /**
   * True only until the first station is on screen. After that the map stays mounted while the next
   * one loads: unmounting it threw away the tile layer and the user's base-layer choice with it, and
   * made every station feel like a fresh page.
   */
  firstLoad = signal(true);

  /** Route lines already fetched. Selecting back and forth should not re-download them. */
  private geometryCache = new Map<number, [number, number][]>();
  private drawnRouteId: number | null = null;

  /**
   * What has been answered this session, newest first, so a mistake noticed several stations later
   * can still be found and taken back. Capped because this is a working list, not an audit log — the
   * database holds the answers themselves.
   */
  history = signal<HistoryEntry[]>([]);
  private static readonly HISTORY_LIMIT = 25;

  /** The most recent answer still standing: what the quick undo button takes back. */
  private lastAction = computed(() => this.history().find((entry) => !entry.reverted) ?? null);
  canUndo = computed(() => this.lastAction() !== null);
  lastActionStation = computed(() => this.lastAction()?.stationName ?? '');

  /**
   * Passing through a station for years before finally getting off there is ordinary, and the two
   * are separate facts on separate dates. Rather than ask every station twice, the second question
   * is opt-in: answer "stopped, and got off later" and the same station stays up for it.
   */
  stage = signal<"stopped" | "entryExit">("stopped");
  /** The trip confirmed in the first stage, resent in the second so its date is not lost. */
  private stoppedTrip = signal<number | null>(null);

  readonly levels = StationVisitLevel;

  /** The date already recorded as the stop, shown as context for the second question. */
  stoppedDate = computed(() => this.instanceById(this.stoppedTrip())?.date ?? null);

  /**
   * The three answers, in a fixed order - only the emphasis moves, so nothing jumps under the cursor.
   *
   * Merely stopping leads because it is what most stations turn out to be: of the visits dated so far,
   * 557 are stopped-only against 172 where the user got on or off. It is also the button nearest the
   * list, which is where the pointer comes from.
   */
  readonly actions: { key: BackfillAction; labelKey: string }[] = [
    { key: "stopped", labelKey: "STATIONS.BACKFILL.ONLY_STOPPED" },
    { key: "entryExit", labelKey: "STATIONS.BACKFILL.GOT_ON_OFF" },
    { key: "stoppedAndLater", labelKey: "STATIONS.BACKFILL.STOPPED_AND_LATER" },
  ];

  /**
   * Which answer the selected trip actually suggests, so the obvious tap is the filled one.
   *
   * A route that starts or ends at the station means you were on the platform, so getting on/off is
   * the likely answer. If instead it merely calls here but a later trip does terminate here, the
   * likely story is "passed through then, got off later" — which is the two-date answer.
   */
  primaryAction = computed<BackfillAction>(() => {
    const selected = this.selectedRow();
    if (!selected) {
      return "stopped";
    }
    if (selected.group.isEndpoint) {
      return "entryExit";
    }
    return this.hasLaterEndpoint(selected.instance.date) ? "stoppedAndLater" : "stopped";
  });

  private selectedRow() {
    const id = this.selected();
    return (
      this.item()
        ?.candidates.flatMap((group) => group.instances.map((instance) => ({ group, instance })))
        .find((row) => row.instance.routeInstanceId === id) ?? null
    );
  }

  private hasLaterEndpoint(afterDate: string): boolean {
    return (this.item()?.candidates ?? []).some(
      (group) => group.isEndpoint && group.instances.some((i) => i.date > afterDate)
    );
  }

  run(action: BackfillAction): Promise<void> {
    switch (action) {
      case "entryExit":
        return this.confirm(StationVisitLevel.EntryExit);
      case "stoppedAndLater":
        return this.confirmStoppedThenAskEntryExit();
      default:
        return this.confirm(StationVisitLevel.Stopped);
    }
  }

  /**
   * How full the region is. Counted from the server's own figures rather than from this session, so it
   * is the truth about the province and not a tally of what was answered since the page loaded.
   */
  regionProgress = computed(() => {
    const region = this.item()?.regionProgress;
    return !region || region.total === 0 ? 0 : (region.dated / region.total) * 100;
  });

  hasWork = computed(() => !!this.item()?.stationId);
  progress = computed(() => {
    const remaining = this.item()?.remaining ?? 0;
    const done = this.done();
    return done + remaining === 0 ? 0 : (done / (done + remaining)) * 100;
  });

  /** Flattened for the radio group: one row per route, plus the rest of a route's trips. */
  rows = computed(() => {
    const groups = this.item()?.candidates ?? [];
    return groups.flatMap((group) =>
      (this.expanded() === group.routeId ? group.instances : group.instances.slice(0, 1)).map(
        (instance) => ({ group, instance })
      )
    );
  });
  expanded = signal<number | null>(null);

  ngOnInit(): void {
    void this.load();
  }

  /**
   * Loads the station to work on: the next one in the queue, or a named one when undo needs to
   * return to the answer it just took back.
   */
  private async load(stationId: number | null = null): Promise<void> {
    this.loading.set(true);
    try {
      const item = await firstValueFrom(this.apiService.getBackfillItem(this.passed(), stationId));
      this.item.set(item);
      this.selected.set(item.suggestedRouteInstanceId);
      this.expanded.set(null);
      this.stage.set("stopped");
      this.stoppedTrip.set(null);
      this.drawnRouteId = null;

      // The default route's line arrives with the station, so the common case needs no second
      // request and the line appears at the same moment the station does.
      if (item.suggestedRouteGeometry) {
        this.geometryCache.set(item.suggestedRouteGeometry.routeId, item.suggestedRouteGeometry.coordinates);
      }

      this.drawStation(item);
      if (item.suggestedRouteInstanceId) {
        await this.drawRoute(this.routeIdFor(item.suggestedRouteInstanceId));
      }
    } finally {
      this.loading.set(false);
      this.firstLoad.set(false);
    }
  }

  /**
   * Leaflet measures its container once, on creation. Here the container is a flex child that only
   * reaches its real size after the surrounding columns lay out — and it changes again when the
   * window crosses the two-column breakpoint — so without this the tiles are drawn for a box the map
   * no longer occupies.
   */
  onMapReady(map: LeafletMap): void {
    // Watched rather than measured once: the container is a flex child sized by the columns around it,
    // so it settles after Leaflet has already drawn, and changes again whenever the window is resized
    // or crosses the two-column breakpoint. Left unhandled it draws tiles for a box it no longer fills.
    this.mapResize?.disconnect();
    this.mapResize = new ResizeObserver(() => map.invalidateSize());
    this.mapResize.observe(map.getContainer());
  }

  ngOnDestroy(): void {
    this.mapResize?.disconnect();
  }

  private mapResize: ResizeObserver | null = null;

  private groupFor(routeInstanceId: number | null): TripCandidateGroup | null {
    return (
      this.item()?.candidates.find((g) =>
        g.instances.some((i) => i.routeInstanceId === routeInstanceId)
      ) ?? null
    );
  }

  private routeIdFor(routeInstanceId: number | null): number | null {
    return this.groupFor(routeInstanceId)?.routeId ?? null;
  }

  private drawStation(item: StationBackfillItem): void {
    if (!item.stationId) {
      this.layers.set([]);
      return;
    }
    const position = new LatLng(item.lattitude, item.longitude);
    this.layers.set([
      circleMarker(position, {
        radius: 8,
        fillColor: "#FF7F00",
        color: "#000",
        weight: 1,
        opacity: 1,
        fillOpacity: 0.9,
      }),
    ]);
    this.bounds.set(boxAround(position));
  }

  /** Seeing the line sweep through the station is the evidence; a lone pin is not. */
  private async drawRoute(routeId: number | null): Promise<void> {
    const item = this.item();
    if (!item || routeId === null || routeId === this.drawnRouteId) {
      // Already the line on screen: redrawing it would only make the map flicker.
      return;
    }
    try {
      let coordinates = this.geometryCache.get(routeId);
      if (!coordinates) {
        const geometry = await firstValueFrom(this.apiService.getBackfillRouteGeometry(routeId));
        coordinates = geometry.coordinates as [number, number][];
        // Kept for the session: clicking back and forth between two candidates is common, and a
        // route's line does not change while you are deciding about it.
        this.geometryCache.set(routeId, coordinates);
      }
      const line = polyline(coordinates, { color: "#1E88E5", weight: 4, opacity: 0.8 });
      this.drawStation(item);
      this.layers.update((existing) => [line, ...existing]);
      this.bounds.set(boxAround(new LatLng(item.lattitude, item.longitude)));
      this.drawnRouteId = routeId;
    } catch {
      // A missing line is not worth blocking the decision on; the pin and dates still stand.
    }
  }

  async select(routeInstanceId: number): Promise<void> {
    this.selected.set(routeInstanceId);
    await this.drawRoute(this.routeIdFor(routeInstanceId));
  }

  toggle(routeId: number): void {
    this.expanded.update((current) => (current === routeId ? null : routeId));
  }

  evidenceOf(group: TripCandidateGroup): "endpoint" | "nearby" {
    return group.isEndpoint ? "endpoint" : "nearby";
  }

  /** The user's own name for the route type, in their language. */
  typeName(group: TripCandidateGroup): string {
    return this.translationService.getNameForItem({
      name: group.routeTypeName,
      nameNL: group.routeTypeNameNL,
    });
  }

  /** Both outcomes are the same decision split two ways, so the level costs no extra tap. */
  async confirm(level: StationVisitLevel): Promise<void> {
    const item = this.item();
    const routeInstanceId = this.selected();
    if (!item || routeInstanceId === null) {
      return;
    }

    this.saving.set(true);
    try {
      await firstValueFrom(
        this.apiService.updateStationVisitDates(item.stationId, {
          firstStoppedDate: null,
          firstStoppedRouteInstanceId: level === StationVisitLevel.Stopped ? routeInstanceId : null,
          firstEntryExitDate: null,
          firstEntryExitRouteInstanceId: level === StationVisitLevel.EntryExit ? routeInstanceId : null,
        })
      );
      this.rememberAnswer(
        level === StationVisitLevel.EntryExit
          ? 'STATIONS.BACKFILL.GOT_ON_OFF'
          : 'STATIONS.BACKFILL.ONLY_STOPPED',
        routeInstanceId
      );
      this.done.update((d) => d + 1);
      await this.load();
    } finally {
      this.saving.set(false);
    }
  }

  /**
   * Records the stop, then stays on this station to ask which later trip you got off on. The stop is
   * saved before the second question, so abandoning half way leaves the answer already given rather
   * than losing it.
   */
  async confirmStoppedThenAskEntryExit(): Promise<void> {
    const item = this.item();
    const routeInstanceId = this.selected();
    if (!item || routeInstanceId === null) {
      return;
    }

    this.saving.set(true);
    try {
      await firstValueFrom(
        this.apiService.updateStationVisitDates(item.stationId, {
          firstStoppedDate: null,
          firstStoppedRouteInstanceId: routeInstanceId,
          firstEntryExitDate: null,
          firstEntryExitRouteInstanceId: null,
        })
      );
      // Remembered here, not only when the second stage finishes: the stopped date is already
      // written, so walking away mid-flow must still be undoable.
      this.rememberAnswer('STATIONS.BACKFILL.ONLY_STOPPED', routeInstanceId);
      this.stoppedTrip.set(routeInstanceId);
      this.stage.set("entryExit");
      await this.selectLikelyEntryExit(routeInstanceId);
    } finally {
      this.saving.set(false);
    }
  }

  /**
   * Getting off happened on a later trip than the one that first merely stopped, so the default
   * looks forward from it — preferring a route that starts or ends here, which is the only evidence
   * of having been on the platform.
   */
  private async selectLikelyEntryExit(stoppedTripId: number): Promise<void> {
    const stopped = this.instanceById(stoppedTripId);
    const groups = this.item()?.candidates ?? [];
    const later = groups
      .flatMap((group) => group.instances.map((instance) => ({ group, instance })))
      .filter((row) => row.instance.routeInstanceId !== stoppedTripId)
      .filter((row) => !stopped || row.instance.date >= stopped.date)
      .sort((a, b) => a.instance.date.localeCompare(b.instance.date));

    const pick = later.find((row) => row.group.isEndpoint) ?? later[0] ?? null;
    this.selected.set(pick?.instance.routeInstanceId ?? null);
    if (pick) {
      await this.drawRoute(pick.group.routeId);
    }
  }

  /** Records getting off, resending the stop so the date confirmed a moment ago survives. */
  async confirmEntryExit(): Promise<void> {
    const item = this.item();
    const routeInstanceId = this.selected();
    const stoppedTripId = this.stoppedTrip();
    if (!item || routeInstanceId === null || stoppedTripId === null) {
      return;
    }

    this.saving.set(true);
    try {
      await firstValueFrom(
        this.apiService.updateStationVisitDates(item.stationId, {
          firstStoppedDate: null,
          firstStoppedRouteInstanceId: stoppedTripId,
          firstEntryExitDate: null,
          firstEntryExitRouteInstanceId: routeInstanceId,
        })
      );
      this.rememberAnswer('STATIONS.BACKFILL.GOT_ON_OFF', routeInstanceId);
      this.done.update((d) => d + 1);
      await this.load();
    } finally {
      this.saving.set(false);
    }
  }

  /**
   * Leaves the stop recorded and moves on without claiming to have got off here. The stopped date
   * from the first stage stands, so it stays undoable — this is the normal end of that flow, not a
   * cancel.
   */
  async skipEntryExit(): Promise<void> {
    const item = this.item();
    if (item) {
      this.rememberAnswer('STATIONS.BACKFILL.ONLY_STOPPED', this.stoppedTrip());
    }
    this.done.update((d) => d + 1);
    await this.load();
  }

  /** Records an answer for the session list, described by what was actually chosen. */
  private rememberAnswer(labelKey: string, routeInstanceId: number | null): void {
    const item = this.item();
    if (!item) {
      return;
    }
    const group = this.groupFor(routeInstanceId);
    this.remember({
      kind: 'dated',
      stationId: item.stationId,
      stationName: item.stationName,
      labelKey,
      date: this.instanceById(routeInstanceId)?.date ?? null,
      // Which trip the date came from. A fast service and a stopping one share the same track, so
      // the route name is what makes picking the wrong one visible after the fact.
      routeName: group?.routeName ?? null,
      routeTypeColour: group?.routeTypeColour ?? null,
      reverted: false,
    });
  }

  private remember(action: UndoableAction): void {
    // Replace any earlier entry for the same station: the list is "what this station ended up as",
    // not every keystroke on the way there — the two-stage flow would otherwise record it twice.
    this.history.update((entries) =>
      [action, ...entries.filter((e) => e.stationId !== action.stationId)].slice(
        0,
        StationBackfillComponent.HISTORY_LIMIT
      )
    );
  }

  /**
   * Takes back the last confirm. Dating is undone by clearing both dates, which puts the visit back
   * in the queue undated; a set-aside station is undone by clearing the flag. Either way the station
   * returns to the position we are still standing at, so reloading shows it again.
   */
  /** Takes back the most recent answer still standing. */
  undo(): Promise<void> {
    const action = this.lastAction();
    return action ? this.revert(action) : Promise.resolve();
  }

  /**
   * Takes back any answer from the session list, not only the last. Dating is undone by clearing
   * both dates, which returns the visit to the queue undated; a set-aside station is undone by
   * clearing the flag.
   */
  async revert(entry: HistoryEntry): Promise<void> {
    if (entry.reverted) {
      return;
    }

    this.saving.set(true);
    try {
      if (entry.kind === "dated") {
        await firstValueFrom(
          this.apiService.updateStationVisitDates(entry.stationId, {
            firstStoppedDate: null,
            firstStoppedRouteInstanceId: null,
            firstEntryExitDate: null,
            firstEntryExitRouteInstanceId: null,
          })
        );
      } else {
        await firstValueFrom(this.apiService.unskipBackfillStation(entry.stationId));
      }

      this.history.update((entries) =>
        entries.map((e) => (e.stationId === entry.stationId ? { ...e, reverted: true } : e))
      );
      this.done.update((d) => Math.max(0, d - 1));
      // Land on the station that was just put back, rather than at the head of the queue: after
      // taking an answer back the user wants to answer it again, not to click past 5,000 stations
      // to reach it. The position is left alone, so carrying on afterwards resumes where they were.
      await this.load(entry.stationId);
    } finally {
      this.saving.set(false);
    }
  }

  private instanceById(routeInstanceId: number | null) {
    if (routeInstanceId === null) {
      return null;
    }
    return (
      this.item()
        ?.candidates.flatMap((g) => g.instances)
        .find((i) => i.routeInstanceId === routeInstanceId) ?? null
    );
  }

  async skip(): Promise<void> {
    const item = this.item();
    if (!item) {
      return;
    }
    this.saving.set(true);
    try {
      await firstValueFrom(this.apiService.skipBackfillStation(item.stationId));
      this.remember({
        kind: 'skipped',
        stationId: item.stationId,
        stationName: item.stationName,
        labelKey: 'STATIONS.BACKFILL.CANNOT_REMEMBER',
        date: null,
        routeName: null,
        routeTypeColour: null,
        reverted: false,
      });
      // Counted as progress like any other answer: it leaves the queue, so leaving `done` alone
      // shrank the total and made the bar jump forward — and undo's decrement had nothing to undo.
      this.done.update((d) => d + 1);
      await this.load();
    } finally {
      this.saving.set(false);
    }
  }

  /** Leaves the station in the queue and moves on — useful when nothing here looks right. */
  async later(): Promise<void> {
    this.passed.update((p) => p + 1);
    await this.load();
  }
}

/** An answer that can be taken back, and enough to say what it was. */
export interface HistoryEntry {
  kind: 'dated' | 'skipped';
  stationId: number;
  stationName: string;
  /** Which answer was given, as a translation key, so the list reads in the user's language. */
  labelKey: string;
  /** The date recorded, where one was. Null for a set-aside station. */
  date: string | null;
  /**
   * The trip the date came from. Shown because the date alone cannot tell a fast service from a
   * stopping one over the same track, and picking the wrong one is the mistake worth catching.
   */
  routeName: string | null;
  /** The route type's own colour, so the entry reads like the candidate row it came from. */
  routeTypeColour: string | null;
  reverted: boolean;
}

type UndoableAction = HistoryEntry;

/** The three answers the first stage can give. */
export type BackfillAction = "entryExit" | "stopped" | "stoppedAndLater";

/**
 * A window of roughly a kilometre around the station. Padding a zero-area bounds keeps it zero-area,
 * which made Leaflet jump to maximum zoom and show one street - the point is to see the line sweep
 * through, so it needs room either side.
 */
function boxAround(position: LatLng): LatLngBounds {
  const delta = 0.01;
  return new LatLngBounds(
    new LatLng(position.lat - delta, position.lng - delta),
    new LatLng(position.lat + delta, position.lng + delta)
  );
}
