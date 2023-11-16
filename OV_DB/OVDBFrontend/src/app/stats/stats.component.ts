import { Component, OnInit, ViewChild } from '@angular/core';
import { ApiService } from '../services/api.service';
import { ChartOptions } from 'chart.js';
import { tileLayer, marker, icon } from 'leaflet';
import * as L from 'leaflet';
import { Map } from '../models/map.model';
import { TranslationService } from '../services/translation.service';
import { TranslateService } from '@ngx-translate/core';
@Component({
  selector: 'app-stats',
  templateUrl: './stats.component.html',
  styleUrls: ['./stats.component.scss']
})
export class StatsComponent implements OnInit {
  @ViewChild('singleChart') singleChart;
  data: any;
  singleData: any;

  loadingMap = false;
  selectedMap: string = null;
  selectedYear: number = null;
  bounds = new L.LatLngBounds(new L.LatLng(50.656245, 2.921360), new L.LatLng(53.604563, 7.428211));
  years = [];
  public lineChartOptions: ChartOptions = {
    responsive: true,
    maintainAspectRatio: false,
    // tooltips: {
    //   enabled: true
    // },
    // scales: {
    //   xAxes: [{
    //     type: 'time',
    //     time: {
    //       tooltipFormat: 'DD-MM-YYYY',
    //       unit: 'month',
    //       displayFormats: {
    //         month: 'MMM\'YY'
    //       }
    //     },
    //   }],
    // }
  };
  public barChartOptions: any = {
    responsive: true,
    maintainAspectRatio: false,
    tooltips: {
      enabled: true
    },
    scales: {
      xAxes: [{
        type: 'time',
        offset: true,
        time: {
          tooltipFormat: 'DD-MM-YYYY',
          unit: 'month',
          displayFormats: {
            month: 'MMM\'YY'
          }
        },
        stacked: true
      }],
      yAxes: [{
        stacked: true
      }]
    },
    pan: {
      enabled: true,
      mode: 'x'
    },
    zoom: {
      enabled: true,
      mode: 'x',
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
          // tslint:disable-next-line: max-line-length
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


  constructor(
    private apiService: ApiService,
    private translationService: TranslationService,
    public translateService: TranslateService,
  ) { }

  ngOnInit(): void {
    this.apiService.getMaps().subscribe(maps => {
      this.maps = maps;
    });
  }

  changeMap(mapGuid: string) {
    this.selectedMap = mapGuid;

    this.apiService.getYears(this.selectedMap).subscribe(years => {
      this.years = years.sort().reverse();
    });
    this.data = null;
    this.layers = [];
    this.tableData = null;
    this.selectedYear = null;
  }

  getData(year?: number) {
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
      console.log(this.translateService.instant('EXTREMES.SOUTH'));
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
      this.bounds = new L.LatLngBounds([data.latMin.lat, data.longMin.long], [data.latMax.lat, data.longMax.long]);
      const rectangle = new L.Rectangle(this.bounds, {
        fill: false
      });
      this.layers.push(rectangle);
      this.loadingMap = false;
    });

  }
  resetZoom() {
    this.singleChart.resetZoom();
  }

  name(item) {
    return this.translationService.getNameForItem(item);
  }

  download() {
    this.apiService.getTripReport(this.selectedMap, this.selectedYear).subscribe(data => {
      saveAs(data, 'tripreport.xlsx');
    });
  }

  export() {
    this.apiService.getCompleteExport(this.selectedMap, this.selectedYear).subscribe(data => {
      saveAs(data, 'export.kml');
    });
  }
}
