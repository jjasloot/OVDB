import { ChangeDetectorRef, Component, OnInit, input, inject, signal, computed, ChangeDetectionStrategy } from "@angular/core";
import { firstValueFrom } from "rxjs";
import {
  LatLngBounds,
  LatLng,
  divIcon,
  circleMarker,
  LeafletEvent
} from "leaflet";
import { ApiService } from "src/app/services/api.service";
import { TranslationService } from "src/app/services/translation.service";
import { MapTileLayersService } from "src/app/services/map-tile-layers.service";
import { LeafletModule } from "@bluehalo/ngx-leaflet";
import { NgClass } from "@angular/common";
import { MatProgressSpinner } from "@angular/material/progress-spinner";
import { MatSnackBar, MatSnackBarConfig } from "@angular/material/snack-bar";
import { MatDialog } from "@angular/material/dialog";
import { TranslateService } from "@ngx-translate/core";
import { createMarkerClusterGroup } from "src/app/leaflet-markercluster-loader";
import { StationVisitDates, StationVisitLevel } from "src/app/models/stationView.model";
import {
  StationVisitDialogComponent,
  StationVisitDialogData,
  StationVisitDialogResult,
} from "../station-visit-dialog/station-visit-dialog.component";
import {
  StationVisitDateDialogComponent,
  StationVisitDateDialogData,
} from "../station-visit-date-dialog/station-visit-date-dialog.component";

/**
 * Three states: red unvisited, green stopped at, blue got on/off. Every visit is at least a stop,
 * so an undated one — including everything predating visit history — is green, not a fourth
 * "unknown" colour. Whether a visit is dated is a separate question from what it means.
 */
function markerStyle(visited: boolean, level: StationVisitLevel | null) {
  if (!visited) {
    return { radius: 4, fillColor: "#FF0000", color: "#000", weight: 1, opacity: 1, fillOpacity: 0.5 };
  }
  const fillColor = level === StationVisitLevel.EntryExit ? "#1E88E5" : "#00C853";
  return { radius: 8, fillColor, color: "#000", weight: 1, opacity: 1, fillOpacity: 0.8 };
}

const PENDING_STYLE = { radius: 6, fillColor: "#FF7F00", color: "#000", weight: 1, opacity: 1, fillOpacity: 0.65 };

/** No duration: a mis-tap is usually noticed long after five seconds have passed. */
const UNDOABLE: MatSnackBarConfig = { verticalPosition: "bottom" };

const SMALL_DIALOG = { maxWidth: "95vw", width: "360px" };

@Component({
    selector: "app-station-map",
    templateUrl: "./station-map.component.html",
    styleUrls: ["./station-map.component.scss"],
    changeDetection: ChangeDetectionStrategy.Eager,
    imports: [
        LeafletModule,
        NgClass,
        MatProgressSpinner,
    ]
})
export class StationMapComponent implements OnInit {
  private apiService = inject(ApiService);
  private translationService = inject(TranslationService);
  private cd = inject(ChangeDetectorRef);
  private mapTileLayersService = inject(MapTileLayersService);
  private snackBar = inject(MatSnackBar);
  private dialog = inject(MatDialog);
  private translateService = inject(TranslateService);

  baseLayers = this.mapTileLayersService.createBaseLayers();
  readonly guid = input<string>();
  options = {
    layers: [this.mapTileLayersService.defaultLayer(this.baseLayers)],
    zoom: 5,
  };
  private _bounds = signal<LatLngBounds | null>(null);
  total = signal<number>(0);
  visited = signal<number>(0);
  names = signal<{ name: any; nameNL: any }>({ name: null, nameNL: null });
  layers = signal<any[]>([]);
  loading = signal(true);

  percentage = computed(() => {
    if (!this.total() || this.visited() == undefined) {
      return "?";
    }
    return Math.round((this.visited() / this.total()) * 1000) / 10;
  });

  get bounds(): LatLngBounds {
    return this._bounds()!;
  }
  set bounds(value: LatLngBounds) {
    if (!!value && value.isValid()) {
      this._bounds.set(value);
    } else {
      this._bounds.set(new LatLngBounds(
        new LatLng(50.656245, 2.92136),
        new LatLng(53.604563, 7.428211)
      ));
    }
  }
  leafletLayersControl = {
    baseLayers: this.baseLayers,
    overlays: {},
  };
  ngOnInit(): void {
    this.getData();
  }

  async getData() {
    this.loading.set(true);

    const text = await firstValueFrom(this.apiService.getStationMap(this.guid()!));
    const parent = this;
    this.total.set(text.total);
    this.visited.set(text.visited);
    this.names.set({
      name: text.name,
      nameNL: text.nameNL,
    });
    const markers = await createMarkerClusterGroup({
      iconCreateFunction: (cluster) => {
        return divIcon({
          html: "<b>" + cluster.getChildCount() + "</b>",
          className: cluster
            .getAllChildMarkers()
            .every((r) => r.feature!.properties.visited)
            ? "green"
            : cluster
                .getAllChildMarkers()
                .every((r) => !r.feature!.properties.visited)
            ? "red"
            : "orange",
        });
      },
      disableClusteringAtZoom: 10,
      maxClusterRadius: 40,
    });
    text.stations.forEach((station) => {
      const marker = circleMarker(
        new LatLng(station.lattitude, station.longitude, station.elevation!),
        markerStyle(station.visited, station.visitLevel)
      );
      marker.feature = {
        properties: {
          id: station.id,
          name: station.name,
          visited: station.visited,
          visitLevel: station.visitLevel,
          firstStoppedDate: station.firstStoppedDate,
          firstEntryExitDate: station.firstEntryExitDate,
        },
        type: "Feature",
        geometry: null!,
      };
      markers.addLayer(marker);
    });
    markers.addEventListener("click", (f: LeafletEvent) => {
      const properties = f.propagatedFrom.feature.properties;
      if (properties.visited) {
        // Already visited: changing it is deliberate, so ask what to do rather than toggling
        // something away on a stray tap.
        void parent.openVisitDialog(f.propagatedFrom);
      } else {
        // A plain tap records the weaker claim; upgrading to entry/exit is an explicit choice
        // offered in the snackbar.
        void parent.markStation(f.propagatedFrom, StationVisitLevel.Stopped);
      }
    });
    this.layers.set([markers]);
    this.bounds = markers.getBounds();
    this.loading.set(false);
  }

  getName(object: { name: string; nameNL: string }) {
    return this.translationService.getNameForItem(object);
  }

  /** Marks or upgrades a station, then reflects the result on the marker. */
  private async markStation(marker: any, level: StationVisitLevel): Promise<void> {
    const properties = marker.feature.properties;
    const wasVisited = properties.visited;
    marker.setStyle(PENDING_STYLE);
    this.refreshClusters();

    try {
      const state = await firstValueFrom(
        this.apiService.updateStation(properties.id, true, level)
      );
      this.applyState(marker, state.visited, state.level, state.firstStoppedDate, state.firstEntryExitDate);
      if (!wasVisited) {
        this.visited.update((v) => v + 1);
      }
      this.offerUpgrade(marker, state.level);
    } catch {
      this.applyState(marker, wasVisited, properties.visitLevel, properties.firstStoppedDate, properties.firstEntryExitDate);
    }
  }

  private async removeStation(marker: any): Promise<void> {
    const properties = marker.feature.properties;
    const previousLevel = properties.visitLevel;
    const previousStopped = properties.firstStoppedDate;
    const previousEntryExit = properties.firstEntryExitDate;
    marker.setStyle(PENDING_STYLE);
    this.refreshClusters();

    try {
      await firstValueFrom(this.apiService.updateStation(properties.id, false));
      this.applyState(marker, false, null, null, null);
      this.visited.update((v) => v - 1);
      // Un-marking discards the dates, so undo re-marks at the level it had.
      this.snackBar
        .open(
          this.translateService.instant('STATIONS.VISIT.REMOVED', { name: properties.name }),
          this.translateService.instant('UNDO'),
          UNDOABLE
        )
        .onAction()
        .subscribe(() => void this.markStation(marker, previousLevel ?? StationVisitLevel.Stopped));
    } catch {
      this.applyState(marker, true, previousLevel, previousStopped, previousEntryExit);
    }
  }

  /**
   * The snackbar does not expire: noticing a mis-tap ten minutes later is the common case, and a
   * five second window is no use for it.
   */
  private offerUpgrade(marker: any, level: StationVisitLevel | null): void {
    if (level === StationVisitLevel.EntryExit) {
      this.snackBar.open(
        this.translateService.instant('STATIONS.VISIT.MARKED_ENTRY_EXIT', { name: marker.feature.properties.name }),
        this.translateService.instant('UNDO'),
        UNDOABLE
      ).onAction().subscribe(() => void this.removeStation(marker));
      return;
    }

    this.snackBar
      .open(
        this.translateService.instant('STATIONS.VISIT.MARKED_STOPPED', { name: marker.feature.properties.name }),
        this.translateService.instant('STATIONS.VISIT.SET_ENTRY_EXIT'),
        UNDOABLE
      )
      .onAction()
      .subscribe(() => void this.markStation(marker, StationVisitLevel.EntryExit));
  }

  private async openVisitDialog(marker: any): Promise<void> {
    const properties = marker.feature.properties;
    const result = await firstValueFrom(
      this.dialog
        .open<StationVisitDialogComponent, StationVisitDialogData, StationVisitDialogResult>(
          StationVisitDialogComponent,
          {
            data: {
              name: properties.name,
              level: properties.visitLevel,
              firstStoppedDate: properties.firstStoppedDate,
              firstEntryExitDate: properties.firstEntryExitDate,
            },
            ...SMALL_DIALOG,
          }
        )
        .afterClosed()
    );

    switch (result) {
      case 'entryExit':
        await this.markStation(marker, StationVisitLevel.EntryExit);
        break;
      case 'stopped':
        await this.markStation(marker, StationVisitLevel.Stopped);
        break;
      case 'remove':
        await this.removeStation(marker);
        break;
      case 'dates':
        await this.openDateDialog(marker);
        break;
    }
  }

  /**
   * Dating is deliberately a separate step from marking. Marking says "I have been here" and can
   * happen from the sofa; a date is a different claim and the user either knows it or picks the
   * trip that supplies it.
   */
  private async openDateDialog(marker: any): Promise<void> {
    const properties = marker.feature.properties;
    const dates = await firstValueFrom(
      this.dialog
        .open<StationVisitDateDialogComponent, StationVisitDateDialogData, StationVisitDates>(
          StationVisitDateDialogComponent,
          {
            data: {
              stationId: properties.id,
              name: properties.name,
              level: properties.visitLevel,
              firstStoppedDate: properties.firstStoppedDate,
              firstStoppedRouteInstanceId: properties.firstStoppedRouteInstanceId ?? null,
              firstEntryExitDate: properties.firstEntryExitDate,
              firstEntryExitRouteInstanceId: properties.firstEntryExitRouteInstanceId ?? null,
            },
            maxWidth: '95vw',
            width: '480px',
          }
        )
        .afterClosed()
    );

    if (!dates) {
      return;
    }

    try {
      const state = await firstValueFrom(this.apiService.updateStationVisitDates(properties.id, dates));
      this.applyState(marker, state.visited, state.level, state.firstStoppedDate, state.firstEntryExitDate);
      marker.feature.properties.firstStoppedRouteInstanceId = state.firstStoppedRouteInstanceId;
      marker.feature.properties.firstEntryExitRouteInstanceId = state.firstEntryExitRouteInstanceId;
    } catch {
      // Leave the marker showing what the server last confirmed rather than an optimistic guess.
    }
  }

  private applyState(
    marker: any,
    visited: boolean,
    level: StationVisitLevel | null,
    firstStoppedDate: string | null,
    firstEntryExitDate: string | null
  ): void {
    marker.feature.properties.visited = visited;
    marker.feature.properties.visitLevel = level;
    marker.feature.properties.firstStoppedDate = firstStoppedDate;
    marker.feature.properties.firstEntryExitDate = firstEntryExitDate;
    marker.setStyle(markerStyle(visited, level));
    this.refreshClusters();
    this.cd.detectChanges();
  }

  private refreshClusters(): void {
    (this.layers()[0] as any)?.refreshClusters?.();
  }
}
