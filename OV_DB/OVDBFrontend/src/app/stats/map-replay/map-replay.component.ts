import { ChangeDetectionStrategy, Component, DestroyRef, OnInit, computed, inject, signal } from '@angular/core';
import { DatePipe, DecimalPipe } from '@angular/common';
import { MatButton, MatIconButton } from '@angular/material/button';
import { MatCard, MatCardContent, MatCardTitle } from '@angular/material/card';
import { MatFormField, MatLabel } from '@angular/material/form-field';
import { MatIcon } from '@angular/material/icon';
import { MatOption } from '@angular/material/core';
import { MatProgressSpinner } from '@angular/material/progress-spinner';
import { MatSelect } from '@angular/material/select';
import { MatSliderModule } from '@angular/material/slider';
import { MatCheckbox } from '@angular/material/checkbox';
import { TranslateModule } from '@ngx-translate/core';
import { LeafletModule } from '@bluehalo/ngx-leaflet';
import { LatLngBounds, LatLngTuple, Layer, latLngBounds, polyline } from 'leaflet';
import { ApiService } from 'src/app/services/api.service';
import { MapTileLayersService } from 'src/app/services/map-tile-layers.service';
import { TranslationService } from 'src/app/services/translation.service';
import { Map } from 'src/app/models/map.model';
import { ReplayRegion, ReplayRoute } from 'src/app/models/replay.model';

const STEP_INTERVAL_MS = 90;

/**
 * How long a gap between two travel days is allowed to take on screen, in ticks. Real time would be
 * unwatchable — years of nothing between two trips — but collapsing every gap to one tick loses the
 * rhythm of the thing entirely, so a gap is paced by its length and then capped.
 */
const DAYS_PER_GAP_TICK = 7;
const MAX_GAP_TICKS = 6;

/** Playback speeds, as multipliers of the base tick interval. */
export const REPLAY_SPEEDS = [0.25, 0.5, 1, 2, 4] as const;

@Component({
  selector: 'app-map-replay',
  templateUrl: './map-replay.component.html',
  styleUrl: './map-replay.component.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [
    DatePipe,
    DecimalPipe,
    MatButton,
    MatIconButton,
    MatCard,
    MatCardTitle,
    MatCardContent,
    MatFormField,
    MatLabel,
    MatIcon,
    MatSelect,
    MatOption,
    MatProgressSpinner,
    MatSliderModule,
    MatCheckbox,
    TranslateModule,
    LeafletModule,
  ],
})
export class MapReplayComponent implements OnInit {
  private apiService = inject(ApiService);
  private mapTileLayersService = inject(MapTileLayersService);
  private translationService = inject(TranslationService);

  maps = signal<Map[]>([]);
  years = signal<number[]>([]);
  selectedMap = signal<string | null>(null);
  selectedYear = signal<number | null>(null);
  routes = signal<ReplayRoute[]>([]);
  loading = signal(false);
  playing = signal(false);
  /** Position along the timeline of dates, not a count of routes. */
  index = signal(0);
  /** Route overrides on by default, since that is what the maps themselves show. */
  useOverrideColours = signal(true);
  speed = signal<number>(1);
  readonly speeds = REPLAY_SPEEDS;

  stoppedDates = signal<number[]>([]);
  private entryExitDates = signal<number[]>([]);

  private timer: number | null = null;

  /**
   * The dates the replay steps through: every day something was first ridden, plus a few filler
   * ticks across long gaps so a two-year break reads as a pause rather than as a jump cut.
   */
  private timeline = computed<number[]>(() => {
    const days = [...new Set(this.routes().map((r) => Date.parse(r.firstDate)))].sort((a, b) => a - b);
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

  /** The moment the replay is currently showing. */
  private cursor = computed(() => this.timeline()[Math.min(this.index(), this.timelineLength())] ?? 0);

  /** Routes are ordered by first ride, so everything up to the cursor is simply a prefix. */
  revealed = computed(() => {
    const cursor = this.cursor();
    const routes = this.routes();
    let count = 0;
    while (count < routes.length && Date.parse(routes[count].firstDate) <= cursor) {
      count++;
    }
    return count;
  });

  private countUpTo(dates: number[], cursor: number): number {
    let count = 0;
    while (count < dates.length && dates[count] <= cursor) {
      count++;
    }
    return count;
  }

  /** Stations known to have been stopped at by now. Includes the ones since upgraded. */
  stationsStopped = computed(() => this.countUpTo(this.stoppedDates(), this.cursor()));

  /** Of those, the ones you had got on or off at by now — the upgrades. */
  stationsEntryExit = computed(() => this.countUpTo(this.entryExitDates(), this.cursor()));

  /** Still only stopped at: what the green part of the bar represents. */
  stationsStoppedOnly = computed(() => this.stationsStopped() - this.stationsEntryExit());

  stoppedPercentage = computed(() => {
    const total = this.stoppedDates().length;
    return total === 0 ? 0 : (this.stationsStopped() / total) * 100;
  });

  entryExitPercentage = computed(() => {
    const total = this.stoppedDates().length;
    return total === 0 ? 0 : (this.stationsEntryExit() / total) * 100;
  });

  private regionData = signal<ReplayRegion[]>([]);

  /**
   * Per-country progress at the cursor. Order is fixed for the whole replay — bars that reorder as
   * they fill are impossible to follow — and each fills towards the country's own station count, so
   * the bars are comparable as coverage rather than as raw totals.
   */
  regionProgress = computed(() => {
    const cursor = this.cursor();
    return this.regionData().map((region) => {
      const stopped = this.countUpTo(this.parsed(region.stoppedDates), cursor);
      const entryExit = this.countUpTo(this.parsed(region.entryExitDates), cursor);
      return {
        regionId: region.regionId,
        name: this.translationService.getNameForItem(region),
        flagEmoji: region.flagEmoji,
        stopped,
        entryExit,
        total: region.totalStations,
        stoppedPercentage: region.totalStations ? (stopped / region.totalStations) * 100 : 0,
        entryExitPercentage: region.totalStations ? (entryExit / region.totalStations) * 100 : 0,
      };
    });
  });

  /** Dates arrive as strings; parsing them once per array keeps the per-tick work to counting. */
  private parsedCache = new Map<string[], number[]>();
  private parsed(dates: string[]): number[] {
    let cached = this.parsedCache.get(dates);
    if (!cached) {
      cached = dates.map((d) => Date.parse(d));
      this.parsedCache.set(dates, cached);
    }
    return cached;
  }

  baseLayers = this.mapTileLayersService.createBaseLayers();
  options = {
    layers: [this.mapTileLayersService.defaultLayer(this.baseLayers)],
    zoom: 7,
    center: [52.1, 5.3] as LatLngTuple,
  };
  leafletLayersControl = { baseLayers: this.baseLayers, overlays: {} };
  // Sensible default until the real extent is known, mirroring the other stats map.
  bounds = signal<LatLngBounds>(latLngBounds([50.656245, 2.92136], [53.604563, 7.428211]));

  // Rebuilt when the colour source changes; revealing routes is then just a slice.
  private polylines = computed<Layer[]>(() => {
    const useOverrides = this.useOverrideColours();
    return this.routes().map((route) =>
      polyline(
        route.coordinates.map((c) => [c[0], c[1]] as LatLngTuple),
        {
          color: (useOverrides ? route.colour : route.routeTypeColour) || '#3388ff',
          weight: 3,
          opacity: 0.85,
        }
      )
    );
  });

  layers = computed<Layer[]>(() => this.polylines().slice(0, this.revealed()));

  currentDate = computed(() => {
    const cursor = this.cursor();
    return cursor ? new Date(cursor).toISOString() : null;
  });

  cumulativeKm = computed(() =>
    this.routes()
      .slice(0, this.revealed())
      .reduce((sum, route) => sum + route.distanceKm, 0)
  );

  constructor() {
    inject(DestroyRef).onDestroy(() => this.stop());
  }

  ngOnInit(): void {
    this.apiService.getMaps().subscribe((maps) => {
      this.maps.set(maps);
      if (maps.length > 0) {
        const defaultMap = maps.find((m) => m.default) ?? maps[0];
        this.changeMap(defaultMap.mapGuid);
      }
    });
  }

  changeMap(mapGuid: string): void {
    this.selectedMap.set(mapGuid);
    this.apiService.getYears(mapGuid).subscribe((years) => {
      this.years.set([...years].sort((a, b) => b - a));
      this.selectedYear.set(null);
      this.load();
    });
  }

  changeYear(year: number): void {
    this.selectedYear.set(year === 0 ? null : year);
    this.load();
  }

  togglePlay(): void {
    if (this.playing()) {
      this.stop();
      return;
    }
    if (this.routes().length === 0) {
      return;
    }
    // Starting from the end would show nothing happening; rewind first.
    if (this.index() >= this.timelineLength()) {
      this.index.set(0);
    }
    this.playing.set(true);
    this.startTimer();
  }

  /** One tick is one step along the timeline, so speed changes the clock, not the granularity. */
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

  toggleOverrideColours(): void {
    this.useOverrideColours.update((on) => !on);
  }

  setIndex(value: number): void {
    this.stop();
    this.index.set(value);
  }

  reset(): void {
    this.stop();
    this.index.set(0);
  }

  private stop(): void {
    if (this.timer !== null) {
      window.clearInterval(this.timer);
      this.timer = null;
    }
    this.playing.set(false);
  }

  private load(): void {
    const map = this.selectedMap();
    if (!map) {
      return;
    }
    this.stop();
    this.loading.set(true);
    this.index.set(0);
    this.apiService.getReplay(map, this.selectedYear()).subscribe({
      next: (replay) => {
        this.routes.set(replay.routes);
        this.stoppedDates.set((replay.stoppedDates ?? []).map((d) => Date.parse(d)));
        this.entryExitDates.set((replay.entryExitDates ?? []).map((d) => Date.parse(d)));
        this.parsedCache.clear();
        this.regionData.set(replay.regions ?? []);
        const bounds = this.boundsFor(replay.routes);
        if (bounds) {
          this.bounds.set(bounds);
        }
        // Opens fully drawn, so the tab is a map of everything until you rewind and play.
        this.index.set(this.timelineLength());
        this.loading.set(false);
      },
      error: () => {
        this.routes.set([]);
        this.stoppedDates.set([]);
        this.entryExitDates.set([]);
        this.regionData.set([]);
        this.loading.set(false);
      },
    });
  }

  private boundsFor(routes: ReplayRoute[]): LatLngBounds | null {
    const points = routes.flatMap((route) =>
      route.coordinates.map((c) => [c[0], c[1]] as LatLngTuple)
    );
    return points.length > 0 ? latLngBounds(points) : null;
  }
}
