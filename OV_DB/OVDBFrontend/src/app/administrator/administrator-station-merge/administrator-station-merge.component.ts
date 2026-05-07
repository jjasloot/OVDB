import { DecimalPipe } from "@angular/common";
import {
  ChangeDetectionStrategy,
  Component,
  computed,
  DestroyRef,
  inject,
  OnInit,
  signal,
} from "@angular/core";
import { takeUntilDestroyed } from "@angular/core/rxjs-interop";
import { FormsModule } from "@angular/forms";
import { MatButton } from "@angular/material/button";
import { MatCard, MatCardContent, MatCardHeader, MatCardTitle } from "@angular/material/card";
import { MatIcon } from "@angular/material/icon";
import { MatOption } from "@angular/material/core";
import { MatProgressSpinner } from "@angular/material/progress-spinner";
import { MatSelect } from "@angular/material/select";
import { MatTooltip } from "@angular/material/tooltip";
import { LeafletModule } from "@bluehalo/ngx-leaflet";
import { divIcon, LatLng, LatLngBounds, Layer, marker, tileLayer } from "leaflet";
import { StationMergeCountry, StationNearbyPair } from "src/app/models/stationMerge.model";
import { ApiService } from "src/app/services/api.service";

/** Returns an HTMLElement whose textContent is set, so Leaflet treats it as safe plain text. */
function safeTooltipContent(text: string): HTMLElement {
  const el = document.createElement("span");
  el.textContent = text;
  return el;
}

@Component({
  selector: "app-administrator-station-merge",
  templateUrl: "./administrator-station-merge.component.html",
  styleUrls: ["./administrator-station-merge.component.scss"],
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [
    FormsModule,
    MatButton,
    MatCard,
    MatCardContent,
    MatCardHeader,
    MatCardTitle,
    MatIcon,
    MatOption,
    MatProgressSpinner,
    MatSelect,
    MatTooltip,
    DecimalPipe,
    LeafletModule,
  ],
})
export class AdministratorStationMergeComponent implements OnInit {
  private apiService = inject(ApiService);
  private destroyRef = inject(DestroyRef);

  regions = signal<StationMergeCountry[]>([]);
  selectedRegionId = signal<number | null>(null);
  currentPair = signal<StationNearbyPair | null>(null);
  totalPairs = signal<number>(0);
  loading = signal<boolean>(false);
  actionInProgress = signal<boolean>(false);

  readonly mapOptions = {
    layers: [
      tileLayer("https://{s}.tile.openstreetmap.org/{z}/{x}/{y}.png", {
        opacity: 0.85,
        attribution: '&copy; <a href="https://www.openstreetmap.org/copyright">OpenStreetMap</a> contributors',
      }),
    ],
    zoom: 17,
  };

  readonly mapLayers = computed<Layer[]>(() => {
    const pair = this.currentPair();
    if (!pair) return [];

    const markerL = marker(new LatLng(pair.station1Lattitude, pair.station1Longitude), {
      icon: divIcon({
        html: '<div class="smerge-marker-l">L</div>',
        className: "smerge-icon",
        iconSize: [28, 28],
        iconAnchor: [14, 14],
      }),
    }).bindTooltip(safeTooltipContent(pair.station1Name || "(unnamed)"), {
      permanent: true,
      direction: "top",
      offset: [0, -14],
    });

    const markerR = marker(new LatLng(pair.station2Lattitude, pair.station2Longitude), {
      icon: divIcon({
        html: '<div class="smerge-marker-r">R</div>',
        className: "smerge-icon",
        iconSize: [28, 28],
        iconAnchor: [14, 14],
      }),
    }).bindTooltip(safeTooltipContent(pair.station2Name || "(unnamed)"), {
      permanent: true,
      direction: "top",
      offset: [0, -14],
    });

    return [markerL, markerR];
  });

  readonly mapBounds = computed<LatLngBounds>(() => {
    const pair = this.currentPair();
    if (!pair) {
      return new LatLngBounds(new LatLng(50.656245, 2.92136), new LatLng(53.604563, 7.428211));
    }
    const bounds = new LatLngBounds(
      new LatLng(pair.station1Lattitude, pair.station1Longitude),
      new LatLng(pair.station2Lattitude, pair.station2Longitude)
    );
    return bounds.isValid() ? bounds.pad(1.5) : bounds;
  });

  ngOnInit(): void {
    this.loadRegions();
  }

  private loadRegions(): void {
    this.apiService
      .getStationMergeCountries()
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe((data) => {
        this.regions.set(data);
      });
  }

  onRegionChange(regionId: number): void {
    this.selectedRegionId.set(regionId);
    this.loadCurrentPair();
  }

  private loadCurrentPair(): void {
    const regionId = this.selectedRegionId();
    if (!regionId) return;
    this.loading.set(true);
    this.apiService
      .getStationMergePairs(regionId, 0, 1)
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe({
        next: (data) => {
          this.totalPairs.set(data.total);
          this.currentPair.set(data.pairs.length > 0 ? data.pairs[0] : null);
          this.loading.set(false);
        },
        error: () => this.loading.set(false),
      });
  }

  merge(keepId: number, deleteId: number): void {
    this.actionInProgress.set(true);
    this.apiService
      .mergeStations(keepId, deleteId)
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe({
        next: () => {
          this.actionInProgress.set(false);
          this.loadCurrentPair();
          this.loadRegions();
        },
        error: () => this.actionInProgress.set(false),
      });
  }

  skip(pair: StationNearbyPair): void {
    this.actionInProgress.set(true);
    this.apiService
      .skipStationPair(pair.station1Id, pair.station2Id)
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe({
        next: () => {
          this.actionInProgress.set(false);
          this.loadCurrentPair();
          this.loadRegions();
        },
        error: () => this.actionInProgress.set(false),
      });
  }

  toggleSpecial(stationId: number, currentSpecial: boolean, side: 1 | 2): void {
    const newSpecial = !currentSpecial;
    this.apiService
      .updateStationAdmin(stationId, newSpecial, false)
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe({
        next: () => {
          const pair = this.currentPair();
          if (!pair) return;
          if (side === 1) {
            this.currentPair.set({ ...pair, station1Special: newSpecial });
          } else {
            this.currentPair.set({ ...pair, station2Special: newSpecial });
          }
        },
        error: () => {
          // Reload pair to reset any optimistic state on failure
          this.loadCurrentPair();
        },
      });
  }

  openInOsm(lat: number, lon: number): string {
    return `https://www.openstreetmap.org/?mlat=${lat}&mlon=${lon}&zoom=18`;
  }
}
