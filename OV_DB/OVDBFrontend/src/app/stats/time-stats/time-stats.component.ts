import { NgClass } from '@angular/common';
import { Component, Signal, viewChild, OnInit, inject, ChangeDetectionStrategy } from '@angular/core';
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
import { LatLngBounds, LatLng, marker, icon, Rectangle, Layer } from 'leaflet';
import { ApiService } from 'src/app/services/api.service';
import { TranslationService } from 'src/app/services/translation.service';
import { MapTileLayersService } from 'src/app/services/map-tile-layers.service';
import { Map } from 'src/app/models/map.model';
import { BaseChartDirective } from 'ng2-charts';
import 'chartjs-adapter-luxon';
import { MatTabsModule } from '@angular/material/tabs';
@Component({
  selector: 'app-time-stats',
  imports: [MatCard, MatCardTitle, MatFormField, MatLabel, MatSelect, MatOption, FormsModule, MatButton, LeafletModule, NgClass, MatProgressSpinner, TranslateModule, BaseChartDirective, MatTabsModule, MatCardContent],
  templateUrl: './time-stats.component.html',
  changeDetection: ChangeDetectionStrategy.Eager,
  styleUrl: './time-stats.component.scss'
})
export class TimeStatsComponent implements OnInit {
  private apiService = inject(ApiService);
  private translationService = inject(TranslationService);
  private mapTileLayersService = inject(MapTileLayersService);
  translateService = inject(TranslateService);

  data: ChartConfiguration['data'] | null = null;
  singleData: any;

  loadingMap = false;
  selectedMap: string | null = null;
  selectedYear: number | null = null;
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
  layers: Layer[] = [];
  baseLayers = this.mapTileLayersService.createBaseLayers();

  options = {
    layers: [this.mapTileLayersService.defaultLayer(this.baseLayers)],
    zoom: 5
  };
  leafletLayersControl = {
    baseLayers: this.baseLayers,
    overlays: {},
  };
  maps: Map[] = [];

  ngOnInit(): void {
    this.apiService.getMaps().subscribe(maps => {
      this.maps = maps;
      // Start with the default (or first) map selected instead of an empty page.
      if (maps.length > 0 && this.selectedMap === null) {
        const defaultMap = maps.find(m => m.default) ?? maps[0];
        this.changeMap(defaultMap.mapGuid);
      }
    });
  }


  changeMap(mapGuid: string) {
    this.selectedMap = mapGuid;

    this.data = null;
    this.layers = [];
    this.tableData = null;
    this.selectedYear = null;
    this.apiService.getYears(mapGuid).subscribe(years => {
      this.years = years.sort().reverse();
      // Preselect the current year (or the most recent one with data).
      if (this.selectedYear === null && this.years.length > 0) {
        const currentYear = new Date().getFullYear();
        this.selectedYear = this.years.includes(currentYear) ? currentYear : this.years[0];
        this.getData(this.selectedYear);
      }
    });
  }

  getData(year?: number | null) {
    if (year === 0) year = null;
    this.apiService.getStatsForGraph(this.selectedMap!, year!).subscribe(stats => {
      this.data = stats.cumulative;
      this.singleData = stats.single;
    });
    this.apiService.getStats(this.selectedMap!, year!).subscribe(data => {
      this.tableData = data;
    });
    this.loadingMap = true;
    this.apiService.getStatsReach(this.selectedMap!, year!).subscribe((data: any) => {
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

  name(item: { name: string, nameNL: string }) {
    return this.translationService.getNameForItem(item);
  }

  download() {
    this.apiService.getTripReport(this.selectedMap!, this.selectedYear!).subscribe(data => {
      saveAs(data as Blob, 'tripreport.xlsx');
    });
  }

  export() {
    this.apiService.getCompleteExport(this.selectedMap!, this.selectedYear!).subscribe(data => {
      saveAs(data as Blob, 'export.kml');
    });
  }
}
