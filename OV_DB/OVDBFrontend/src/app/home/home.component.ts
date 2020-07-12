import { Component, OnInit } from '@angular/core';
import { AuthenticationService } from '../services/authentication.service';
import { ApiService } from '../services/api.service';
import { Map } from '../models/map.model';
import { Router } from '@angular/router';
@Component({
  selector: 'app-home',
  templateUrl: './home.component.html',
  styleUrls: ['./home.component.scss']
})
export class HomeComponent implements OnInit {
  maps: Map[];

  constructor(
    private authService: AuthenticationService,
    private router: Router,
    private apiService: ApiService) { }

  ngOnInit() {
    if (this.isLoggedIn) {
      this.apiService.getMaps().subscribe(maps => {
        this.maps = maps;
      });
    }
  }

  get isLoggedIn() {
    return this.authService.isLoggedIn;
  }

  view(map: Map) {
    this.router.navigate(['/map', map.mapGuid]);
  }
  getLink(map: Map) {
    return location.origin + '/link/' + map.sharingLinkName;
  }
}
