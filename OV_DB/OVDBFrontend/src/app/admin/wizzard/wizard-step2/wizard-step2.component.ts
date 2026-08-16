import { Component, OnInit, inject, ChangeDetectionStrategy } from "@angular/core";
import { ActivatedRoute, Router } from "@angular/router";
import { ApiService } from "src/app/services/api.service";
import { MapTileLayersService } from "src/app/services/map-tile-layers.service";
import { WizardStepsComponent } from "../wizard-steps/wizard-steps.component";
import { OSMDataLine } from "src/app/models/osmDataLine.model";
import { LatLngBounds, Layer, geoJSON } from "leaflet";
import { OSMLineStop } from "src/app/models/osmLineStop.model";
import {
  StationSuggestionsComponent,
  StationSuggestionsDialogData,
} from "src/app/stations/station-suggestions/station-suggestions.component";
import { saveAs } from "file-saver";
import { TranslateService, TranslateModule } from "@ngx-translate/core";
import { MatDialog } from "@angular/material/dialog";
import { AreYouSureDialogComponent } from "src/app/are-you-sure-dialog/are-you-sure-dialog.component";
import { Moment } from "moment";
import moment from "moment";
import { MatIconButton, MatButton } from "@angular/material/button";
import { MatIcon } from "@angular/material/icon";
import { MatCard } from "@angular/material/card";
import { MatProgressSpinner } from "@angular/material/progress-spinner";
import { LeafletModule } from "@bluehalo/ngx-leaflet";
import { MatList, MatListItem } from "@angular/material/list";
import { MatChipListbox, MatChipOption } from "@angular/material/chips";
import { NgClass } from "@angular/common";
import { CdkCopyToClipboard } from "@angular/cdk/clipboard";
import { TrawellingTripContext } from "src/app/models/traewelling.model";
import { TrawellingContextCardComponent } from "src/app/traewelling/context-card/traewelling-context-card.component";
import { STANDARD_DIALOG } from "src/app/constants/dialog-sizes";

@Component({
  selector: "app-wizard-step2",
  templateUrl: "./wizard-step2.component.html",
  styleUrls: ["./wizard-step2.component.scss"],
  changeDetection: ChangeDetectionStrategy.Eager,
  imports: [
    WizardStepsComponent,
    MatIconButton,
    MatIcon,
    MatCard,
    MatProgressSpinner,
    LeafletModule,
    MatList,
    MatListItem,
    MatChipListbox,
    MatChipOption,
    MatButton,
    NgClass,
    TranslateModule,
    CdkCopyToClipboard,
    TrawellingContextCardComponent
  ]
})
export class WizzardStep2Component implements OnInit {
  private activatedRoute = inject(ActivatedRoute);
  private apiService = inject(ApiService);
  private translateService = inject(TranslateService);
  private dialog = inject(MatDialog);
  private router = inject(Router);
  private mapTileLayersService = inject(MapTileLayersService);

  id!: string;
  data!: OSMDataLine;

  options = {
    layers: [this.mapTileLayersService.createThemedLayer(0.5)],
    zoom: 5,
  };
  leafletLayersControl = {};
  layers: Layer[] = [];
  bounds!: LatLngBounds;
  stops!: OSMLineStop[];
  loading = false;
  error = false;
  from!: number;
  to!: number;
  dateTime: Moment | null = null;
  fromTraewelling = false;
  trawellingTripData: TrawellingTripContext | null = null;
  constructor() {
    this.activatedRoute.params.subscribe((p) => (this.id = p.id));
    this.activatedRoute.queryParamMap.subscribe((p) => {
      if (p.has("date")) {
        this.dateTime = moment.unix(+p.get("date")!);
      } else {
        this.dateTime = null;
      }

      // Check if coming from Träwelling
      if (p.has('traewellingTripId')) {
        this.fromTraewelling = true;
        const tripDataStr = sessionStorage.getItem('traewellingTripContext');
        if (tripDataStr) {
          const trawellingTripData = JSON.parse(tripDataStr) as TrawellingTripContext;
          if (trawellingTripData.tripId === +p.get('traewellingTripId')!) {
            // If the IDs match, use the data
            this.trawellingTripData = trawellingTripData;
          }
        }
      }
    });
  }

  ngOnInit(): void {
    this.loadLine();
  }

  loadLine(): void {
    this.loading = true;
    this.error = false;
    this.apiService
      .importerGetLine(this.id, undefined, undefined, this.dateTime ?? undefined)
      .subscribe({
        next: (data) => {
          this.data = data;
          this.apiService.importerGetStops(this.id, this.dateTime ?? undefined).subscribe({
            next: (stops) => {
              this.loading = false;
              this.stops = stops;
              this.from = this.stops[0].id;
              this.to = this.stops[this.stops.length - 1].id;
            },
            error: () => {
              this.error = true;
              this.loading = false;
            },
          });
          this.addTrackToMap();
        },
        // Without this the spinner ran forever when the line itself failed to load.
        error: () => {
          this.error = true;
          this.loading = false;
        },
      });
  }

  goback() {
    this.router.navigate(["/", "admin", "wizard"]);
  }
  save() {
    const dialogRef = this.dialog.open(AreYouSureDialogComponent, {
      ...STANDARD_DIALOG,
      data: {
        item: this.translateService.instant("IMPORTER.ADD"),
      },
    });
    dialogRef.afterClosed().subscribe((result: boolean) => {
      if (result) {
        this.loading = true;

        this.apiService.importerAddRoute(this.data).subscribe(
          (route) => {
            // Offer the stations this relation calls at before leaving the page: the stops came
            // back with the import and are not stored, so this is the only chance to act on them.
            this.offerStationSuggestions(() => this.goToRoute(route));
          },
          () => {
            this.error = true;
            this.loading = false;
          }
        );
      }
    });
  }

  private goToRoute(route: { routeId: number }) {
    // If this comes from Träwelling, navigate to route edit with trip data pre-populated
    if (this.fromTraewelling && this.trawellingTripData) {
      this.router.navigate(["/", "admin", "routes", route.routeId], {
        queryParams: { traewellingTripId: this.trawellingTripData.tripId }
      });
    } else {
      this.router.navigate(["/", "admin", "routes", route.routeId]);
    }
  }

  /**
   * Shows the stations the relation says the train calls at that are not marked visited, then
   * continues. Unlike a Träwelling import these carry no date — the route has no trip on it yet —
   * so marking one leaves an undated visit for the backfill to date later.
   */
  private offerStationSuggestions(done: () => void) {
    const stops = this.data.stops ?? [];
    if (!stops.length) {
      done();
      return;
    }

    this.apiService.getSuggestionsFromStops(stops).subscribe({
      next: (stations) => {
        if (!stations.length) {
          done();
          return;
        }
        this.dialog
          .open<StationSuggestionsComponent, StationSuggestionsDialogData>(StationSuggestionsComponent, {
            maxWidth: "95vw",
            width: "560px",
            data: { tripName: this.data.name, routeInstanceId: null, stations },
          })
          .afterClosed()
          .subscribe(() => done());
      },
      // Suggestions are a bonus; never let them hold up the import.
      error: () => done(),
    });
  }

  export() {
    const blob = new Blob([JSON.stringify(this.data.geoJson)], {
      type: "application/json",
    });
    saveAs(blob, this.id + ".geojson");
  }
  setFrom(id: number) {
    this.from = id;
  }

  setTo(id: number) {
    this.to = id;
  }

  showFrom(index: number) {
    const toIndex = this.lastIndex(this.stops, this.to);
    return index < toIndex;
  }
  showTo(index: number) {
    const fromIndex = this.stops.findIndex((t) => t.id === this.from);
    return index > fromIndex;
  }
  included(index: number) {
    const toIndex = this.lastIndex(this.stops, this.to);
    const fromIndex = this.stops.findIndex((t) => t.id === this.from);
    return index >= fromIndex && index <= toIndex;
  }
  addTrackToMap() {
    if (!this.data.geoJson) {
      this.layers = [];
    }
    const track = geoJSON(this.data.geoJson as any, {
      style: (feature) => {
        return {
          color: "#0000FF",
          weight: 3,
        };
      },
    });
    this.layers = [track];
    this.bounds = track.getBounds();
  }

  cut() {
    this.loading = true;

    this.apiService
      .importerGetLine(this.id, this.from, this.to, this.dateTime ?? undefined)
      .subscribe(
        (data) => {
          this.data = data;
          this.addTrackToMap();
          this.loading = false;
        },
        () => {
          this.error = true;
          this.loading = false;
        }
      );
  }
  uncut() {
    this.loading = true;

    this.apiService
      .importerGetLine(this.id, undefined, undefined, this.dateTime ?? undefined)
      .subscribe(
        (data) => {
          this.data = data;
          this.addTrackToMap();
          this.from = this.stops[0].id;
          this.to = this.stops[this.stops.length - 1].id;
          this.loading = false;
        },
        () => {
          this.error = true;
          this.loading = false;
        }
      );
  }

  lastIndex(list: OSMLineStop[], id: number) {
    return list.reduceRight((acc, t, idx) => {
      if (acc === -1 && t.id === id) {
        return idx;
      }
      return acc;
    }, -1);
  }
}
