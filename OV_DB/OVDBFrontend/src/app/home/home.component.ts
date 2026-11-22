import { DatePipe, DecimalPipe } from "@angular/common";
import { RouteInstanceListDTO } from "src/app/models/routeInstanceList.model";
import { injectLinkCopied } from "src/app/utils/link-copied";
import { Component, OnInit, inject, DestroyRef, signal, ChangeDetectionStrategy } from "@angular/core";
import { AuthenticationService } from "../services/authentication.service";
import { ApiService } from "../services/api.service";
import { Map } from "../models/map.model";
import { Router, RouterLink } from "@angular/router";
import { StationMap } from "../models/stationMap.model";
import { TranslationService } from "../services/translation.service";
import { MatButton, MatIconButton } from "@angular/material/button";
import {
  MatCard,
  MatCardHeader,
  MatCardTitle,
  MatCardContent,
} from "@angular/material/card";
import { MatList, MatListItem } from "@angular/material/list";
import { CdkCopyToClipboard } from "@angular/cdk/clipboard";
import { MatIcon } from "@angular/material/icon";
import { MatProgressSpinner } from "@angular/material/progress-spinner";
import { TranslateModule } from "@ngx-translate/core";
import { takeUntilDestroyed } from "@angular/core/rxjs-interop";
import { DemoMapComponent } from "./demo-map/demo-map.component";
@Component({
  selector: "app-home",
  templateUrl: "./home.component.html",
  styleUrls: ["./home.component.scss"],
  changeDetection: ChangeDetectionStrategy.Eager,
  imports: [
    DecimalPipe,
    DatePipe,
    MatButton,
    RouterLink,
    MatCard,
    MatCardHeader,
    MatCardTitle,
    MatCardContent,
    MatList,
    MatListItem,
    CdkCopyToClipboard,
    MatIconButton,
    MatIcon,
    MatProgressSpinner,
    TranslateModule,
    DemoMapComponent,
  ],
})
export class HomeComponent implements OnInit {
  private authService = inject(AuthenticationService);
  private router = inject(Router);
  private translationService = inject(TranslationService);
  private apiService = inject(ApiService);
  private destroyRef = inject(DestroyRef);
  linkCopied = injectLinkCopied();

  maps = signal<Map[]>([]);
  // This year's trips and km per map guid; a map without trips this year is absent.
  mapSummary = signal<Record<string, { trips: number; distanceKm: number }>>({});
  readonly currentYear = new Date().getFullYear();
  recentTrips = signal<RouteInstanceListDTO[]>([]);
  loading = 0;
  stationMaps = signal<StationMap[]>([]);

  ngOnInit() {
    // Load data if already logged in
    if (this.isLoggedIn) {
      this.loadData();
    }

    // Subscribe to login state changes to reload data when user logs in
    this.authService.isLoggedIn$.pipe(
      takeUntilDestroyed(this.destroyRef)
    ).subscribe(isLoggedIn => {
      if (isLoggedIn) {
        this.loadData();
      }
    });
  }

  private loadData() {
    this.loading++;
    this.apiService.getMaps().subscribe({
      next: (maps) => {
        this.maps.set(maps);
        this.loading--;
      },
      error: () => this.loading--,
    });
    // Extra information only (no spinner): the last few trips, newest first.
    this.apiService.getAllRouteInstances(0, 5, "date", true).subscribe((page) => this.recentTrips.set(page.instances));
    // Extra information only: no spinner, and a failure just leaves the figures out.
    this.apiService.getMapsSummary().subscribe((rows) =>
      this.mapSummary.set(Object.fromEntries(rows.map((r) => [r.mapGuid, r])))
    );
    this.loading++;
    this.apiService.listStationMaps().subscribe({
      next: (maps) => {
        this.stationMaps.set(maps);
        this.loading--;
      },
      error: () => this.loading--,
    });
  }

  get isLoggedIn() {
    return this.authService.isLoggedIn;
  }

  getName(item: any) {
    return this.translationService.getNameForItem(item);
  }

  view(map: Map) {
    const years = map.completed ? null : this.getCurrentYear();
    this.router.navigate(["/map", map.mapGuid], {
      queryParams: years ? { years } : {},
    });
  }

  getCurrentYear() {
    return new Date().getFullYear();
  }

  getLink(map: Map) {
    return location.origin + "/link/" + map.sharingLinkName;
  }

  viewStation(map: StationMap) {
    this.router.navigate(["/stations/map", map.mapGuid]);
  }
  getLinkStation(map: StationMap) {
    return location.origin + "/stations/link/" + map.sharingLinkName;
  }
}
