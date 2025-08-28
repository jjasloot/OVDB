import { NgClass } from '@angular/common';
import { Component, Signal, viewChild, OnInit, inject } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { MatButton } from '@angular/material/button';
import { MatCard, MatCardContent, MatCardTitle } from '@angular/material/card';
import { MatOption } from '@angular/material/core';
import { MatFormField, MatLabel } from '@angular/material/form-field';
import { MatProgressSpinner } from '@angular/material/progress-spinner';
import { MatSelect } from '@angular/material/select';
import { LeafletModule } from '@bluehalo/ngx-leaflet';
import { TranslateModule, TranslateService } from '@ngx-translate/core';
import { ChartConfiguration } from 'chart.js';
import saveAs from 'file-saver';
import { LatLngBounds, LatLng, tileLayer, marker, icon, Rectangle } from 'leaflet';
import { ApiService } from 'src/app/services/api.service';
import { TranslationService } from 'src/app/services/translation.service';
import { Map } from 'src/app/models/map.model';
import { BaseChartDirective } from 'ng2-charts';
import 'chartjs-adapter-luxon';
import { MatTabsModule } from '@angular/material/tabs';
@Component({
  selector: 'app-time-stats',
  imports: [MatCard, MatCardTitle, MatFormField, MatLabel, MatSelect, MatOption, FormsModule, MatButton, LeafletModule, NgClass, MatProgressSpinner, TranslateModule, BaseChartDirective, MatTabsModule, MatCardContent],
  templateUrl: './time-stats.component.html',
  styleUrl: './time-stats.component.scss'
})
export class TimeStatsComponent implements OnInit {
  private apiService = inject(ApiService);
  private translationService = inject(TranslationService);
  translateService = inject(TranslateService);

  data: ChartConfiguration['data'];
  singleData: any;

  loadingMap = false;
  selectedMap: string = null;
  selectedYear: number = null;
  bounds = new LatLngBounds(new LatLng(50.656245, 2.921360), new LatLng(53.604563, 7.428211));
  years: number[] = [];
  public lineChartOptions: ChartConfiguration['options'] = {
    responsive: true,
    maintainAspectRatio: false,
    scales: {
      x: {
        type: 'time',
        time: {
          tooltipFormat: 'DD',
          unit: 'month',
          displayFormats: {
            month: 'MMM yyyy'
          }
        },
      },
      y: { type: 'linear', beginAtZero: true, stacked: false }
    }
  };
  public barChartOptions: ChartConfiguration['options'] = {
    responsive: true,
    maintainAspectRatio: false,
    scales: {
      x: {
        type: 'timeseries',
        offset: true,
        time: {
          tooltipFormat: 'DD',
          unit: 'month',
          displayFormats: {
            month: 'MMM yyyy'
          }
        },
      },
      y: {
        stacked: true
      }
    },
  };
  tableData: any;
  layers = [];
  baseLayers =
    {
      OpenStreetMap: tileLayer('https://{s}.tile.openstreetmap.org/{z}/{x}/{y}.png',
        { opacity: 0.8, attribution: '&copy; <a href="https://www.openstreetmap.org/copyright">OpenStreetMap</a> contributors' }),
      'OpenStreetMap Mat': tileLayer('https://{s}.tile.openstreetmap.org/{z}/{x}/{y}.png',
        { opacity: 0.5, attribution: '&copy; <a href="https://www.openstreetmap.org/copyright">OpenStreetMap</a> contributors' }),
      'Esri WorldTopoMap': tileLayer('https://server.arcgisonline.com/ArcGIS/rest/services/World_Topo_Map/MapServer/tile/{z}/{y}/{x}',
        {
          opacity: 0.65,
                   attribution: 'Tiles &copy; Esri &mdash; Esri, DeLorme, NAVTEQ, TomTom, Intermap, iPC, USGS, FAO, NPS, NRCAN, GeoBase, Kadaster NL, Ordnance Survey, Esri Japan, METI, Esri China (Hong Kong), and the GIS User Community'
        })
    };

  options = {
    layers: [
      this.baseLayers['OpenStreetMap Mat']
    ],
    zoom: 5
  };
  leafletLayersControl = {
    baseLayers: this.baseLayers,
    // overlays: this.layers
  };
  maps: Map[];

  ngOnInit(): void {
    this.apiService.getMaps().subscribe(maps => {
      this.maps = maps;
    });
  }


  changeMap(mapGuid: string) {
    this.selectedMap = mapGuid;

    this.apiService.getYears(mapGuid).subscribe(years => {
      this.years = years.sort().reverse();
    });
    this.data = null;
    this.layers = [];
    this.tableData = null;
    this.selectedYear = null;
  }

  getData(year?: number) {
    if (year === 0) year = null;
    this.apiService.getStatsForGraph(this.selectedMap, year).subscribe(stats => {
      this.data = stats.cumulative;
      this.singleData = stats.single;
    });
    this.apiService.getStats(this.selectedMap, year).subscribe(data => {
      this.tableData = data;
    });
    this.loadingMap = true;
    this.apiService.getStatsReach(this.selectedMap, year).subscribe((data: any) => {
      this.layers = [];
      const latMin = marker([data.latMin.lat, data.latMin.long], {
        title: 'LatMin', icon: icon({
          iconSize: [25, 41],
          iconAnchor: [13, 41],
          iconUrl: 'assets/marker-icon.png',
          shadowUrl: 'assets/marker-shadow.png'
        })
      });
      let popup = `<h2>${this.translateService.instant('EXTREMES.SOUTH')}</h2>`;
      popup += '<p>Latitude: ' + data.latMin.lat + '<br>';
      popup += 'Longitude: ' + data.latMin.long + '<br>';
      popup += 'Route: ' + data.latMin.route.name + '</p>';
      latMin.bindPopup(popup);
      this.layers.push(latMin);
      const latMax = marker([data.latMax.lat, data.latMax.long], {
        title: 'latMax', icon: icon({
          iconSize: [25, 41],
          iconAnchor: [13, 41],
          iconUrl: 'assets/marker-icon.png',
          shadowUrl: 'assets/marker-shadow.png'
        })
      });
      popup = `<h2>${this.translateService.instant('EXTREMES.NORTH')}</h2>`;
      popup += '<p>Latitude: ' + data.latMax.lat + '<br>';
      popup += 'Longitude: ' + data.latMax.long + '<br>';
      popup += 'Route: ' + data.latMax.route.name + '</p>';
      latMax.bindPopup(popup);
      this.layers.push(latMax);
      const longMin = marker([data.longMin.lat, data.longMin.long], {
        title: 'longMin', icon: icon({
          iconSize: [25, 41],
          iconAnchor: [13, 41],
          iconUrl: 'assets/marker-icon.png',
          shadowUrl: 'assets/marker-shadow.png'
        })
      });
      popup = `<h2>${this.translateService.instant('EXTREMES.WEST')}</h2>`;
      popup += '<p>Latitude: ' + data.longMin.lat + '<br>';
      popup += 'Longitude: ' + data.longMin.long + '<br>';
      popup += 'Route: ' + data.longMin.route.name + '</p>';
      longMin.bindPopup(popup);
      this.layers.push(longMin);
      const longMax = marker([data.longMax.lat, data.longMax.long], {
        title: 'longMax', icon: icon({
          iconSize: [25, 41],
          iconAnchor: [13, 41],
          iconUrl: 'assets/marker-icon.png',
          shadowUrl: 'assets/marker-shadow.png'
        })
      });
      popup = `<h2>${this.translateService.instant('EXTREMES.EAST')}</h2>`;
      popup += '<p>Latitude: ' + data.longMax.lat + '<br>';
      popup += 'Longitude: ' + data.longMax.long + '<br>';
      popup += 'Route: ' + data.longMax.route.name + '</p>';
      longMax.bindPopup(popup);
      this.layers.push(longMax);
      this.bounds = new LatLngBounds([data.latMin.lat, data.longMin.long], [data.latMax.lat, data.longMax.long]);
      const rectangle = new Rectangle(this.bounds, {
        fill: false
      });
      this.layers.push(rectangle);
      this.loadingMap = false;
    });

  }

  name(item) {
    return this.translationService.getNameForItem(item);
  }

  download() {
    this.apiService.getTripReport(this.selectedMap, this.selectedYear).subscribe(data => {
      saveAs(data as Blob, 'tripreport.xlsx');
    });
  }

  export() {
    this.apiService.getCompleteExport(this.selectedMap, this.selectedYear).subscribe(data => {
      saveAs(data as Blob, 'export.kml');
    });
  }
}
