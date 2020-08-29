import { Component, OnInit } from '@angular/core';
import { ActivatedRoute, Router } from '@angular/router';
import { ApiService } from 'src/app/services/api.service';
import { OSMDataLine } from 'src/app/models/osmDataLine.model';
import { tileLayer } from 'leaflet';
import * as L from 'leaflet';
import { OSMLineStop } from 'src/app/models/osmLineStop.model';
import { saveAs } from 'file-saver';
import { TranslateService } from '@ngx-translate/core';
import { MatDialog } from '@angular/material/dialog';
import { AreYouSureDialogComponent } from 'src/app/are-you-sure-dialog/are-you-sure-dialog.component';
import { Moment } from 'moment';
import * as moment from 'moment';

@Component({
  selector: 'app-wizard-step2',
  templateUrl: './wizard-step2.component.html',
  styleUrls: ['./wizard-step2.component.scss']
})
export class WizzardStep2Component implements OnInit {
  id: string;
  data: OSMDataLine;

  options = {
    layers: [
      tileLayer('https://{s}.tile.openstreetmap.org/{z}/{x}/{y}.png',
        { opacity: 0.5, attribution: '&copy; <a href="https://www.openstreetmap.org/copyright">OpenStreetMap</a> contributors' })
    ],
    zoom: 5
  };
  leafletLayersControl = {
  };
  layers = [];
  bounds: L.LatLngBounds;
  stops: OSMLineStop[];
  loading = false;
  error = false;
  from: number;
  to: number;
  dateTime: Moment;
  constructor(
    private activatedRoute: ActivatedRoute,
    private apiService: ApiService,
    private translateService: TranslateService,
    private dialog: MatDialog,
    private router: Router) {
    this.activatedRoute.params.subscribe(p => this.id = p.id);
    this.activatedRoute.queryParamMap.subscribe(p => {
      if (p.has('date')) {
        this.dateTime = moment.unix(+p.get('date'));
      }
      else {
        this.dateTime = null;
      }
    });

  }

  ngOnInit(): void {
    this.loading = true;
    this.apiService.importerGetLine(this.id, null, null, this.dateTime).subscribe(data => {
      this.data = data;
      this.apiService.importerGetStops(this.id, this.dateTime).subscribe(stops => {
        this.loading = false;
        this.stops = stops;
        this.from = this.stops[0].id;
        this.to = this.stops[this.stops.length - 1].id;
      }, () => { this.error = true; this.loading = false; });
      this.addTrackToMap();

    });

  }

  goback() {
    this.router.navigate(['/', 'admin', 'wizard']);
  }
  save() {
    const dialogRef = this.dialog.open(AreYouSureDialogComponent, {
      width: '50%',
      data: {
        item: this.translateService.instant('IMPORTER.ADD')
      }
    });
    dialogRef.afterClosed().subscribe((result: boolean) => {
      if (!!result) {
        this.loading = true;

        this.apiService.importerAddRoute(this.data).subscribe(route => {
          this.router.navigate(['/', 'admin', 'routes', route.routeId]);
        }, () => { this.error = true; this.loading = false; });
      }
    });
  }

  export() {
    const blob = new Blob([JSON.stringify(this.data.geoJson)], { type: 'application/json' });
    saveAs(blob, this.id + '.geojson');
  }
  setFrom(id: number) {
    this.from = id;
  }

  setTo(id: number) {
    this.to = id;
  }

  showFrom(id: number) {
    const index = this.stops.findIndex(t => t.id === id);
    const toIndex = this.stops.findIndex(t => t.id === this.to);
    return index < toIndex;
  }
  showTo(id: number) {
    const index = this.stops.findIndex(t => t.id === id);
    const fromIndex = this.stops.findIndex(t => t.id === this.from);
    return index > fromIndex;
  }
  included(id: number) {
    const toIndex = this.stops.findIndex(t => t.id === this.to);
    const fromIndex = this.stops.findIndex(t => t.id === this.from);
    const index = this.stops.findIndex(t => t.id === id);
    return index >= fromIndex && index <= toIndex;
  }
  addTrackToMap() {
    if (!this.data.geoJson) {
      this.layers = [];
    }
    const track = L.geoJSON(this.data.geoJson as any, {
      style: feature => {
        return {
          color: '#0000FF',
          weight: 3
        };
      }
    });
    this.layers = [track];
    this.bounds = track.getBounds();
  }


  cut() {
    this.loading = true;

    this.apiService.importerGetLine(this.id, this.from, this.to, this.dateTime).subscribe(data => {
      this.data = data;
      this.addTrackToMap();
      this.loading = false;
    }, () => { this.error = true; this.loading = false; });
  }
  uncut() {
    this.loading = true;

    this.apiService.importerGetLine(this.id, null, null, this.dateTime).subscribe(data => {
      this.data = data;
      this.addTrackToMap();
      this.from = this.stops[0].id;
      this.to = this.stops[this.stops.length - 1].id;
      this.loading = false;

    }, () => { this.error = true; this.loading = false; });
  }
}
