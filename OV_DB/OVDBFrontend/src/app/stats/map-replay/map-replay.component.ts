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
import { TranslateModule } from '@ngx-translate/core';
import { LeafletModule } from '@bluehalo/ngx-leaflet';
import { LatLngBounds, LatLngTuple, Layer, latLngBounds, polyline } from 'leaflet';
import { ApiService } from 'src/app/services/api.service';
import { MapTileLayersService } from 'src/app/services/map-tile-layers.service';
import { TranslationService } from 'src/app/services/translation.service';
import { Map } from 'src/app/models/map.model';
import { ReplayRoute } from 'src/app/models/replay.model';

/** Roughly how many animation steps the whole replay should take, regardless of route count. */
const TARGET_STEPS = 150;
const STEP_INTERVAL_MS = 90;

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
  /** How many routes are currently revealed. */
  index = signal(0);

  private timer: number | null = null;

  baseLayers = this.mapTileLayersService.createBaseLayers();
  options = {
    layers: [this.mapTileLayersService.defaultLayer(this.baseLayers)],
    zoom: 7,
    center: [52.1, 5.3] as LatLngTuple,
  };
  leafletLayersControl = { baseLayers: this.baseLayers, overlays: {} };
  // Sensible default until the real extent is known, mirroring the other stats map.
  bounds = signal<LatLngBounds>(latLngBounds([50.656245, 2.92136], [53.604563, 7.428211]));

  // Built once per data load; revealing routes is then just a slice, not a rebuild.
  private polylines = computed<Layer[]>(() =>
    this.routes().map((route) =>
      polyline(
        route.coordinates.map((c) => [c[0], c[1]] as LatLngTuple),
        { color: route.colour || '#3388ff', weight: 3, opacity: 0.85 }
      )
    )
  );

  layers = computed<Layer[]>(() => this.polylines().slice(0, this.index()));

  currentDate = computed(() => {
    const routes = this.routes();
    const index = this.index();
    return index > 0 ? routes[index - 1].firstDate : (routes[0]?.firstDate ?? null);
  });

  cumulativeKm = computed(() =>
    this.routes()
      .slice(0, this.index())
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
    if (this.index() >= this.routes().length) {
      this.index.set(0);
    }
    this.playing.set(true);
    const step = Math.max(1, Math.ceil(this.routes().length / TARGET_STEPS));
    this.timer = window.setInterval(() => {
      const next = this.index() + step;
      if (next >= this.routes().length) {
        this.index.set(this.routes().length);
        this.stop();
      } else {
        this.index.set(next);
      }
    }, STEP_INTERVAL_MS);
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
        const bounds = this.boundsFor(replay.routes);
        if (bounds) {
          this.bounds.set(bounds);
        }
        this.index.set(replay.routes.length);
        this.loading.set(false);
      },
      error: () => {
        this.routes.set([]);
        this.loading.set(false);
      },
    });
  }

  private boundsFor(routes: ReplayRoute[]): LatLngBounds | null {
    // No flatMap: the frontend tsconfig still targets es2018.
    const points: LatLngTuple[] = [];
    for (const route of routes) {
      for (const coordinate of route.coordinates) {
        points.push([coordinate[0], coordinate[1]]);
      }
    }
    return points.length > 0 ? latLngBounds(points) : null;
  }
}
