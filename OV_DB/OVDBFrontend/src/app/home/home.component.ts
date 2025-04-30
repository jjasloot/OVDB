import { Component, OnInit } from "@angular/core";
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
  MatCardActions,
} from "@angular/material/card";
import { MatList, MatListItem } from "@angular/material/list";
import { CdkCopyToClipboard } from "@angular/cdk/clipboard";
import { MatIcon } from "@angular/material/icon";
import { MatProgressSpinner } from "@angular/material/progress-spinner";
import { TranslateModule } from "@ngx-translate/core";
import { UsedOperatorsComponent } from "../used-operators/used-operators.component";
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
    MatCardActions,
    MatList,
    MatListItem,
    CdkCopyToClipboard,
    MatIconButton,
    MatIcon,
    MatProgressSpinner,
    TranslateModule,
    UsedOperatorsComponent
],
})
export class HomeComponent implements OnInit {
  maps: Map[];
  loading = 0;
  stationMaps: StationMap[];
  constructor(
    private authService: AuthenticationService,
    private router: Router,
    private translationService: TranslationService,
    private apiService: ApiService
  ) {}

  ngOnInit() {
    if (this.isLoggedIn) {
      this.loading++;
      this.apiService.getMaps().subscribe((maps) => {
        this.maps = maps;
        this.loading--;
      });
      this.loading++;
      this.apiService.listStationMaps().subscribe((maps) => {
        this.stationMaps = maps;
        this.loading--;
      });
    }
  }

  get isLoggedIn() {
    return this.authService.isLoggedIn;
  }

  getName(item: any) {
    return this.translationService.getNameForItem(item);
  }

  view(map: Map) {
    this.router.navigate(["/map", map.mapGuid], {
      queryParams: { years: this.getCurrentYear() },
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
