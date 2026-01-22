import { Component, OnInit, inject, DestroyRef, signal } from "@angular/core";
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
@Component({
  selector: "app-home",
  templateUrl: "./home.component.html",
  styleUrls: ["./home.component.scss"],
  imports: [
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
  ],
})
export class HomeComponent implements OnInit {
  private authService = inject(AuthenticationService);
  private router = inject(Router);
  private translationService = inject(TranslationService);
  private apiService = inject(ApiService);
  private destroyRef = inject(DestroyRef);

  maps = signal<Map[]>([]);
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
      if (isLoggedIn && !this.maps && !this.stationMaps) {
        this.loadData();
      }
    });
  }

  private loadData() {
    this.loading++;
    this.apiService.getMaps().subscribe(
      (maps) => {
        this.maps.set(maps);
        this.loading--;
      },
      (error) => {
        // Optionally, handle/log the error here
        this.loading--;
      }
    );
    this.loading++;
    this.apiService.listStationMaps().subscribe(
      (maps) => {
        this.stationMaps.set(maps);
        this.loading--;
      },
      (error) => {
        // Optionally, handle/log the error here
        this.loading--;
      }
    );
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
