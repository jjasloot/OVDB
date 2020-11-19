import { Component, OnInit } from '@angular/core';
import { AuthenticationService } from '../services/authentication.service';
import { ApiService } from '../services/api.service';
import { Map } from '../models/map.model';
import { Router } from '@angular/router';
import { StationMap } from '../models/stationMap.model';
import { TranslationService } from '../services/translation.service';
@Component({
  selector: 'app-home',
  templateUrl: './home.component.html',
  styleUrls: ['./home.component.scss']
})
export class HomeComponent implements OnInit {
  maps: Map[];
  loading = 0;
  stationMaps: StationMap[];
  constructor(
    private authService: AuthenticationService,
    private router: Router,
    private translationService: TranslationService,
    private apiService: ApiService) { }

  ngOnInit() {
    if (this.isLoggedIn) {
      this.loading++;
      this.apiService.getMaps().subscribe(maps => {
        this.maps = maps;
        this.loading--;
      });
      this.loading++;
      this.apiService.listStationMapsAdmin().subscribe(maps => {
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
    this.router.navigate(['/map', map.mapGuid]);
  }
  getLink(map: Map) {
    return location.origin + '/link/' + map.sharingLinkName;
  }

  viewStation(map: Map) {
    this.router.navigate(['/stations/map', map.mapGuid]);
  }
  getLinkStation(map: Map) {
    return location.origin + '/stations/link/' + map.sharingLinkName;
  }
}
