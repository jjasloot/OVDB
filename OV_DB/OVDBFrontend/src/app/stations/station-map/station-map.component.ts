import { ChangeDetectorRef, Component, Input, OnInit } from '@angular/core';
import { TranslateService } from '@ngx-translate/core';
import { LatLngBounds, LatLng, markerClusterGroup, divIcon, circleMarker, LeafletEvent, MarkerClusterGroup } from 'leaflet';
import { tileLayer } from 'leaflet';
import { ApiService } from 'src/app/services/api.service';
import { TranslationService } from 'src/app/services/translation.service';
import { LeafletModule } from '@asymmetrik/ngx-leaflet';
import { NgClass } from '@angular/common';
import { MatProgressSpinner } from '@angular/material/progress-spinner';
@Component({
    selector: 'app-station-map',
    templateUrl: './station-map.component.html',
    styleUrls: ['./station-map.component.scss'],
    standalone: true,
    imports: [LeafletModule, NgClass, MatProgressSpinner]
})
export class StationMapComponent implements OnInit {
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
  @Input() guid: string;
  options = {
    layers: [
      this.baseLayers['OpenStreetMap Mat']
    ],
    zoom: 5
  };
  private _bounds: LatLngBounds;
  total: number;
  visited: number;
  names: { name: any; nameNL: any; };
  get bounds(): LatLngBounds {
    return this._bounds;
  }
  set bounds(value: LatLngBounds) {
    if (!!value && value.isValid()) {
      this._bounds = value;
    } else {
      this.bounds = new LatLngBounds(new LatLng(50.656245, 2.921360), new LatLng(53.604563, 7.428211));
    }
  }
  leafletLayersControl = {
    baseLayers: this.baseLayers,
    // overlays: this.layers
  };
  layers = [];

  loading = true;
  constructor(
    private apiService: ApiService,
    private translateService: TranslateService,
    private translationService: TranslationService,
    private cd: ChangeDetectorRef
  ) { }
  get percentage() {
    if (!this.total || this.visited == undefined) {
      return '?';
    }
    return Math.round(this.visited / this.total * 1000) / 10;
  }
  ngOnInit(): void {
    this.getData();
  }

  async getData() {
    this.loading = true;

    const text = await this.apiService.getStationMap(this.guid).toPromise();
    const parent = this;
    this.total = text.total;
    this.visited = text.visited;
    this.names = {
      name: text.name,
      nameNL: text.nameNL
    }
    var markers = markerClusterGroup({
      iconCreateFunction: cluster => {
        return divIcon({
          html: '<b>' + cluster.getChildCount() + '</b>', className:
            cluster.getAllChildMarkers().every(r => r.feature.properties.visited) ? 'green' :
              cluster.getAllChildMarkers().every(r => !r.feature.properties.visited) ? 'red' : 'orange'
        });
      }, disableClusteringAtZoom: 10, maxClusterRadius: 40
    });
    text.stations.forEach(station => {
      const marker = circleMarker(new LatLng(station.lattitude, station.longitude, station.elevation), {
        radius: station.visited ? 8 : 4,
        fillColor: station.visited ? '#00FF00' : '#FF0000',
        color: "#000",
        weight: 1,
        opacity: 1,
        fillOpacity: station.visited ? 0.8 : 0.5,
      });
      marker.feature = {
        properties:
        {
          id: station.id,
          visited: station.visited
        },
        type: 'Feature',
        geometry: null
      }
      markers.addLayer(marker);
    })
    markers.addEventListener('click', async (f: LeafletEvent) => {
      console.log(f, f.propagatedFrom);
      if (!f.propagatedFrom.feature.properties.visited) {
        f.propagatedFrom.feature.properties.visited = true;
      } else {
        f.propagatedFrom.feature.properties.visited = false;
      }
      console.log(f.propagatedFrom.feature.properties)
      f.propagatedFrom.setStyle({
        fillColor: '#FF7F00',
        fillOpacity: 0.65,
        radius: 6
      });
      (this.layers[0] as MarkerClusterGroup).refreshClusters();
      await parent.apiService.updateStation(f.propagatedFrom.feature.properties.id, f.propagatedFrom.feature.properties.visited).toPromise();
      if (f.propagatedFrom.feature.properties.visited) {
        parent.visited++;
      } else {
        parent.visited--;
      }
      parent.cd.detectChanges();
      console.log(f.propagatedFrom);
      f.propagatedFrom.setStyle({
        fillColor: f.propagatedFrom.feature.properties.visited ? '#00FF00' : '#FF0000',
        fillOpacity: f.propagatedFrom.feature.properties.visited ? 0.8 : 0.5,
        radius: f.propagatedFrom.feature.properties.visited ? 8 : 4
      });
      (this.layers[0] as MarkerClusterGroup).refreshClusters();
    });
    this.layers = [markers];
    this.bounds = markers.getBounds();
    this.loading = false;
  }


  getName(object) {
    return this.translationService.getNameForItem(object);
  }
}
