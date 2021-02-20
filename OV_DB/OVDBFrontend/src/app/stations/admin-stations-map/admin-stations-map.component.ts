import { ChangeDetectorRef, Component, Input, OnInit } from '@angular/core';
import { TranslateService } from '@ngx-translate/core';
import * as L from 'leaflet';
import { tileLayer } from 'leaflet';
import { ApiService } from 'src/app/services/api.service';

@Component({
  selector: 'app-admin-stations-map',
  templateUrl: './admin-stations-map.component.html',
  styleUrls: ['./admin-stations-map.component.scss']
})
export class AdminStationsMapComponent implements OnInit {

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
  private _bounds: L.LatLngBounds;
  total: number;
  visited: number;
  get bounds(): L.LatLngBounds {
    return this._bounds;
  }
  set bounds(value: L.LatLngBounds) {
    if (!!value && value.isValid()) {
      this._bounds = value;
    } else {
      this.bounds = new L.LatLngBounds(new L.LatLng(50.656245, 2.921360), new L.LatLng(53.604563, 7.428211));
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
    private cd: ChangeDetectorRef
  ) { }
  get percentage() {
    if (this.total === 0) {
      return '?';
    }
    return Math.round(this.visited / this.total * 1000) / 10;
  }
  ngOnInit(): void {
    this.getData();
  }

  async getData() {
    this.loading = true;

    const text = await this.apiService.getStationsAdminMap().toPromise();
    const parent = this;
    var markers = L.markerClusterGroup({
      iconCreateFunction: cluster => {
        return L.divIcon({
          html: '<b>' + cluster.getChildCount() + '</b>', className:
            cluster.getAllChildMarkers().every(r => r.feature.properties.visited) ? 'green' :
              cluster.getAllChildMarkers().every(r => !r.feature.properties.visited) ? 'red' : 'orange'
        });
      }, disableClusteringAtZoom: 10, maxClusterRadius: 40
    });
    text.forEach(station => {
      const marker = L.circleMarker(new L.LatLng(station.lattitude, station.longitude, station.elevation), {
        radius: 6,
        fillColor: station.hidden ? '#FF0000' : (station.special ? '#0000FF' : '#00FF00'),
        color: "#000",
        weight: 1,
        opacity: 1,
        fillOpacity: 0.65
      });
      marker.feature = {
        properties:
        {
          id: station.id,
          hidden: station.hidden,
          special: station.special
        },
        type: 'Feature',
        geometry: null
      }
      markers.addLayer(marker);
    })
    markers.addEventListener('click', async (f) => {
      if (!f.propagatedFrom.feature.properties.special && !f.propagatedFrom.feature.properties.hidden) {
        f.propagatedFrom.feature.properties.special = true;
        f.propagatedFrom.feature.properties.hidden = false;
      } else {
        if (!!f.propagatedFrom.feature.properties.special && !f.propagatedFrom.feature.properties.hidden) {
          f.propagatedFrom.feature.properties.special = false;
          f.propagatedFrom.feature.properties.hidden = true;
        }
        else {
          if (!f.propagatedFrom.feature.properties.special && !!f.propagatedFrom.feature.properties.hidden) {
            f.propagatedFrom.feature.properties.special = false;
            f.propagatedFrom.feature.properties.hidden = false;
          }
        }
      }
      f.target.setStyle({
        fillColor: '#FF7F00',
      });
      await parent.apiService.updateStationAdmin(
        f.propagatedFrom.feature.properties.id,
        f.propagatedFrom.feature.properties.special,
        f.propagatedFrom.feature.properties.hidden).toPromise();
      if (f.propagatedFrom.feature.properties.visited) {
        parent.visited++;
      } else {
        parent.visited--;
      }
      parent.cd.detectChanges();
      f.propagatedFrom.setStyle({
        fillColor: f.propagatedFrom.feature.properties.hidden ? '#FF0000' : (f.propagatedFrom.feature.properties.special ? '#0000FF' : '#00FF00'),
      });

    })

    this.layers = [markers];
    this.bounds = markers.getBounds();
    this.loading = false;
  }

}
