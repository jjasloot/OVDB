import { ChangeDetectionStrategy, Component, OnInit, computed, inject, signal } from "@angular/core";
import { LatLng, LatLngBounds, Layer, circleMarker, polyline } from "leaflet";
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
export class StationBackfillComponent implements OnInit {
  private apiService = inject(ApiService);
  private mapTileLayersService = inject(MapTileLayersService);

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

  readonly levels = StationVisitLevel;

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

  private async load(): Promise<void> {
    this.loading.set(true);
    try {
      const item = await firstValueFrom(this.apiService.getBackfillItem(this.passed()));
      this.item.set(item);
      this.selected.set(item.suggestedRouteInstanceId);
      this.expanded.set(null);
      this.drawStation(item);
      if (item.suggestedRouteInstanceId) {
        await this.drawRoute(this.routeIdFor(item.suggestedRouteInstanceId));
      }
    } finally {
      this.loading.set(false);
    }
  }

  private routeIdFor(routeInstanceId: number | null): number | null {
    const group = this.item()?.candidates.find((g) =>
      g.instances.some((i) => i.routeInstanceId === routeInstanceId)
    );
    return group?.routeId ?? null;
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
    this.bounds.set(new LatLngBounds(position, position).pad(0.02));
  }

  /** Seeing the line sweep through the station is the evidence; a lone pin is not. */
  private async drawRoute(routeId: number | null): Promise<void> {
    const item = this.item();
    if (!item || routeId === null) {
      return;
    }
    try {
      const geometry = await firstValueFrom(this.apiService.getBackfillRouteGeometry(routeId));
      const line = polyline(geometry.coordinates, { color: "#1E88E5", weight: 4, opacity: 0.8 });
      this.drawStation(item);
      this.layers.update((existing) => [line, ...existing]);
      const station = new LatLng(item.lattitude, item.longitude);
      this.bounds.set(new LatLngBounds(station, station).pad(0.02));
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
      this.done.update((d) => d + 1);
      await this.load();
    } finally {
      this.saving.set(false);
    }
  }

  async skip(): Promise<void> {
    const item = this.item();
    if (!item) {
      return;
    }
    this.saving.set(true);
    try {
      await firstValueFrom(this.apiService.skipBackfillStation(item.stationId));
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
