import {
  ChangeDetectionStrategy,
  Component,
  DestroyRef,
  OnInit,
  computed,
  effect,
  inject,
  signal,
} from '@angular/core';
import { DatePipe, DecimalPipe } from '@angular/common';
import { MatButton, MatIconButton } from '@angular/material/button';
import { MatButtonToggleModule } from '@angular/material/button-toggle';
import { MatCard, MatCardContent } from '@angular/material/card';
import { MatCheckbox } from '@angular/material/checkbox';
import { MatFormField, MatLabel } from '@angular/material/form-field';
import { MatIcon } from '@angular/material/icon';
import { MatOption } from '@angular/material/core';
import { MatProgressSpinner } from '@angular/material/progress-spinner';
import { MatSelect } from '@angular/material/select';
import { MatSliderModule } from '@angular/material/slider';
import { TranslateModule } from '@ngx-translate/core';
import { LeafletModule } from '@bluehalo/ngx-leaflet';
import {
  Circle,
  CircleMarker,
  LatLngBounds,
  LatLngTuple,
  LayerGroup,
  Map as LeafletMap,
  Renderer,
  canvas,
  circle,
  circleMarker,
  latLngBounds,
  layerGroup,
} from 'leaflet';
import { ApiService } from 'src/app/services/api.service';
import { MapTileLayersService } from 'src/app/services/map-tile-layers.service';
import { StationReplayStation } from 'src/app/models/replay.model';
import { REPLAY_SPEEDS } from 'src/app/stats/map-replay/map-replay.component';
import { CoverageSteps, ReplayStation, buildCoverage, radiusAt } from './station-coverage';

const STEP_INTERVAL_MS = 90;

/** The same gap pacing the route replay uses, so the two timelines feel like one thing. */
const DAYS_PER_GAP_TICK = 7;
const MAX_GAP_TICKS = 6;


/** Which claim the replay is about. Both are visits; the second is the stricter set. */
export type ReplayLevel = 'stopped' | 'entryExit';

/** The two colours the station map already uses for the same two claims. */
const COLOURS: Record<ReplayLevel, string> = {
  stopped: '#00C853',
  entryExit: '#1E88E5',
};


/**
 * Station visits played back over the map they belong to, and the same visits as coverage: a circle
 * around every visited station reaching as far as its nearest *unvisited* station, so a circle only
 * ever contains stations that have been visited — and the circles grow and merge as the gaps
 * between them are filled in.
 */
@Component({
  selector: 'app-station-replay',
  templateUrl: './station-replay.component.html',
  styleUrl: './station-replay.component.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [
    DatePipe,
    DecimalPipe,
    MatButton,
    MatIconButton,
    MatButtonToggleModule,
    MatCard,
    MatCardContent,
    MatCheckbox,
    MatFormField,
    MatLabel,
    MatIcon,
    MatSelect,
    MatOption,
    MatProgressSpinner,
    MatSliderModule,
    TranslateModule,
    LeafletModule,
  ],
})
export class StationReplayComponent implements OnInit {
  private apiService = inject(ApiService);
  private mapTileLayersService = inject(MapTileLayersService);

  loading = signal(true);
  playing = signal(false);
  index = signal(0);
  speed = signal<number>(1);
  readonly speeds = REPLAY_SPEEDS;
  level = signal<ReplayLevel>('stopped');
  showCoverage = signal(true);

  private stations = signal<StationReplayStation[]>([]);

  private timer: number | null = null;
  private leafletMap: LeafletMap | null = null;
  private dotGroup: LayerGroup | null = null;
  private coverageGroup: LayerGroup | null = null;
  private dotRenderer: Renderer | null = null;
  private coverageRenderer: Renderer | null = null;
  private dots = new Map<number, CircleMarker>();
  private circles = new Map<number, Circle>();

  baseLayers = this.mapTileLayersService.createBaseLayers();
  options = {
    layers: [this.mapTileLayersService.defaultLayer(this.baseLayers)],
    zoom: 7,
    center: [52.1, 5.3] as LatLngTuple,
  };
  leafletLayersControl = { baseLayers: this.baseLayers, overlays: {} };
  bounds = signal<LatLngBounds>(latLngBounds([50.656245, 2.92136], [53.604563, 7.428211]));

  /**
   * Every station as the current level sees it. Switching level re-dates the whole set rather than
   * filtering it: a station stopped at in 2019 and got off at in 2023 sits at a different point in
   * each of the two timelines.
   */
  private replayStations = computed<ReplayStation[]>(() => {
    const level = this.level();
    return this.stations().map((station) => ({
      id: station.id,
      name: station.name,
      lat: station.lat,
      lon: station.lon,
      at: enteredAt(station, level),
    }));
  });

  /** Visited at this level, in the order they were reached. Undated ones lead, being `-Infinity`. */
  private ordered = computed<ReplayStation[]>(() =>
    this.replayStations()
      .filter((s) => s.at !== Infinity)
      .sort((a, b) => a.at - b.at)
  );

  dated = computed(() => this.ordered().filter((s) => s.at !== -Infinity).length);
  undated = computed(() => this.ordered().filter((s) => s.at === -Infinity).length);
  totalStations = computed(() => this.stations().length);

  /** One tick per day something was first reached, plus filler ticks so long gaps read as pauses. */
  private timeline = computed<number[]>(() => {
    const days = [...new Set(this.ordered().map((s) => s.at))]
      .filter((d) => Number.isFinite(d))
      .sort((a, b) => a - b);
    if (days.length === 0) {
      return [];
    }
    const ticks: number[] = [days[0]];
    for (let i = 1; i < days.length; i++) {
      const gapDays = (days[i] - days[i - 1]) / 86_400_000;
      const fillers = Math.min(Math.floor(gapDays / DAYS_PER_GAP_TICK), MAX_GAP_TICKS);
      for (let f = 1; f <= fillers; f++) {
        ticks.push(days[i - 1] + ((days[i] - days[i - 1]) * f) / (fillers + 1));
      }
      ticks.push(days[i]);
    }
    return ticks;
  });

  timelineLength = computed(() => Math.max(0, this.timeline().length - 1));

  private cursor = computed(
    () => this.timeline()[Math.min(this.index(), this.timelineLength())] ?? 0
  );

  currentDate = computed(() => {
    const cursor = this.cursor();
    return cursor > 0 ? new Date(cursor).toISOString() : null;
  });

  /** Ordered by date, so everything reached by the cursor is a prefix of the list. */
  revealed = computed(() => {
    const cursor = this.cursor();
    const ordered = this.ordered();
    let count = 0;
    while (count < ordered.length && ordered[count].at <= cursor) {
      count++;
    }
    return count;
  });

  percentage = computed(() => {
    const total = this.totalStations();
    return total === 0 ? 0 : (this.revealed() / total) * 100;
  });

  constructor() {
    inject(DestroyRef).onDestroy(() => this.stop());
    // Leaflet is driven directly rather than through [leafletLayers]: a tick moves a handful of
    // stations, and rebuilding thousands of layers to show that is the difference between an
    // animation and a slideshow.
    effect(() => this.render());
  }

  ngOnInit(): void {
    this.load();
  }

  onMapReady(map: LeafletMap): void {
    this.leafletMap = map;
    // One pane holding every circle, faded as a whole. Fading each circle instead makes overlaps
    // darker than their surroundings, and the whole point of the view is that they merge.
    const pane = map.createPane('stationCoverage');
    pane.style.opacity = '0.32';
    pane.style.zIndex = '450';
    // Canvas, not SVG: this draws every visited station twice over, and thousands of SVG paths
    // stop being an animation. The renderer lives in the faded pane, so the merged look survives.
    this.coverageRenderer = canvas({ pane: 'stationCoverage', padding: 0.5 });
    this.dotRenderer = canvas({ padding: 0.5 });
    this.coverageGroup = layerGroup().addTo(map);
    this.dotGroup = layerGroup().addTo(map);
    // The card around the map sizes itself from its content, so Leaflet measured too early.
    setTimeout(() => map.invalidateSize(), 0);
    this.rebuild();
  }

  private load(): void {
    this.stop();
    this.loading.set(true);
    this.index.set(0);
    this.apiService.getStationReplay().subscribe({
      next: (replay) => {
        this.stations.set(replay.stations ?? []);
        const visited = this.ordered();
        if (visited.length > 0) {
          this.bounds.set(latLngBounds(visited.map((s) => [s.lat, s.lon] as LatLngTuple)));
        }
        this.rebuild();
        // Opens fully drawn: the tab is the finished picture until you rewind and play it.
        this.index.set(this.timelineLength());
        this.loading.set(false);
      },
      error: () => {
        this.stations.set([]);
        this.loading.set(false);
      },
    });
  }

  /** Layer objects are made once per data set and per level, then only shown, hidden and resized. */
  private rebuild(): void {
    if (!this.dotGroup || !this.coverageGroup) {
      return;
    }
    this.dotGroup.clearLayers();
    this.coverageGroup.clearLayers();
    this.dots.clear();
    this.circles.clear();
    const colour = COLOURS[this.level()];
    for (const station of this.ordered()) {
      const isUndated = station.at === -Infinity;
      this.dots.set(
        station.id,
        circleMarker([station.lat, station.lon], {
          renderer: this.dotRenderer ?? undefined,
          radius: isUndated ? 4 : 5,
          // An undated visit is drawn hollow: it belongs on the map, but it cannot claim a moment
          // on the timeline, and a solid dot appearing at tick zero would claim exactly that.
          fillColor: isUndated ? '#FFFFFF' : colour,
          color: colour,
          weight: isUndated ? 2 : 1,
          opacity: 1,
          fillOpacity: 0.9,
        }).bindTooltip(station.name)
      );
      this.circles.set(
        station.id,
        circle([station.lat, station.lon], {
          renderer: this.coverageRenderer ?? undefined,
          radius: 0,
          pane: 'stationCoverage',
          stroke: false,
          fillColor: colour,
          fillOpacity: 1,
        })
      );
    }
    this.render();
  }

  /**
   * Puts the map where the cursor says it should be. Both layers are diffed rather than rebuilt:
   * membership moves by a handful of stations per tick, and a radius that has not changed is not
   * worth a path update.
   */
  private render(): void {
    const ordered = this.ordered();
    const revealed = this.revealed();
    const showCoverage = this.showCoverage();
    const dotGroup = this.dotGroup;
    const coverageGroup = this.coverageGroup;
    if (!dotGroup || !coverageGroup) {
      return;
    }

    for (let i = 0; i < ordered.length; i++) {
      const dot = this.dots.get(ordered[i].id);
      if (!dot) {
        continue;
      }
      const shouldShow = i < revealed;
      if (shouldShow && !dotGroup.hasLayer(dot)) {
        dotGroup.addLayer(dot);
      } else if (!shouldShow && dotGroup.hasLayer(dot)) {
        dotGroup.removeLayer(dot);
      }
    }

    if (!showCoverage) {
      coverageGroup.clearLayers();
      return;
    }

    const coverage = this.coverage();
    const cursor = this.cursor();
    for (let i = 0; i < ordered.length; i++) {
      const shape = this.circles.get(ordered[i].id);
      if (!shape) {
        continue;
      }
      const radius = i < revealed ? radiusAt(coverage.get(ordered[i].id), cursor) : 0;
      // A station whose nearest neighbour of any kind is still unvisited has nothing to enclose.
      if (i >= revealed || radius <= 0) {
        if (coverageGroup.hasLayer(shape)) {
          coverageGroup.removeLayer(shape);
        }
        continue;
      }
      if (Math.abs(shape.getRadius() - radius) > 1) {
        shape.setRadius(radius);
      }
      if (!coverageGroup.hasLayer(shape)) {
        coverageGroup.addLayer(shape);
      }
    }
  }

  /**
   * Every circle's whole history, worked out once per level rather than per frame. See
   * {@link buildCoverage}.
   */
  private coverage = computed(() => buildCoverage(this.replayStations(), this.ordered()));

  togglePlay(): void {
    if (this.playing()) {
      this.stop();
      return;
    }
    if (this.timelineLength() === 0) {
      return;
    }
    // Starting from the end would show nothing happening; rewind first.
    if (this.index() >= this.timelineLength()) {
      this.index.set(0);
    }
    this.playing.set(true);
    this.startTimer();
  }

  private startTimer(): void {
    this.timer = window.setInterval(() => {
      const next = this.index() + 1;
      if (next >= this.timelineLength()) {
        this.index.set(this.timelineLength());
        this.stop();
      } else {
        this.index.set(next);
      }
    }, STEP_INTERVAL_MS / this.speed());
  }

  setSpeed(speed: number): void {
    this.speed.set(speed);
    // Re-arm at the new interval without losing the user's place.
    if (this.playing() && this.timer !== null) {
      window.clearInterval(this.timer);
      this.startTimer();
    }
  }

  setIndex(value: number): void {
    this.stop();
    this.index.set(value);
  }

  reset(): void {
    this.stop();
    this.index.set(0);
  }

  /** Switching level re-dates every station, so the timeline and the layers both start over. */
  setLevel(level: ReplayLevel): void {
    if (level === this.level()) {
      return;
    }
    this.stop();
    this.level.set(level);
    this.rebuild();
    this.index.set(this.timelineLength());
  }

  toggleCoverage(): void {
    this.showCoverage.update((on) => !on);
  }

  private stop(): void {
    if (this.timer !== null) {
      window.clearInterval(this.timer);
      this.timer = null;
    }
    this.playing.set(false);
  }
}

/** When a station joins the set the given level is about, as a sortable number. */
function enteredAt(station: StationReplayStation, level: ReplayLevel): number {
  if (!station.visited) {
    return Infinity;
  }
  if (level === 'entryExit') {
    // Getting on or off *is* recorded as that date, so a visit without one is a stop and nothing
    // more. There is no such thing as an undated got-on/off visit.
    return station.entryExit ? Date.parse(station.entryExit) : Infinity;
  }
  // Every visit is at least a stop, so a missing date here means "yes, but when is unknown".
  return station.stopped ? Date.parse(station.stopped) : -Infinity;
}

