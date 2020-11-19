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
    const track = L.geoJSON(text as any, {
      pointToLayer(feature, latlng) {
        return L.circleMarker(latlng, {
          radius: 6,
          fillColor: feature.properties.hidden ? '#FF0000' : (feature.properties.special ? '#0000FF' : '#00FF00'),
          color: "#000",
          weight: 1,
          opacity: 1,
          fillOpacity: 0.65
        });

      },
      onEachFeature(feature, layer) {
        layer.addEventListener('click', async (f) => {
          if (!feature.properties.special && !feature.properties.hidden) {
            feature.properties.special = true;
            feature.properties.hidden = false;
          } else {
            if (!!feature.properties.special && !feature.properties.hidden) {
              feature.properties.special = false;
              feature.properties.hidden = true;
            }
            else {
              if (!feature.properties.special && !!feature.properties.hidden) {
                feature.properties.special = false;
                feature.properties.hidden = false;
              }
            }
          }
          f.target.setStyle({
            fillColor: '#FF7F00',
          });
          await parent.apiService.updateStationAdmin(feature.properties.id, feature.properties.special, feature.properties.hidden).toPromise();
          if (feature.properties.visited) {
            parent.visited++;
          } else {
            parent.visited--;
          }
          parent.cd.detectChanges();
          f.target.setStyle({
            fillColor: feature.properties.hidden ? '#FF0000' : (feature.properties.special ? '#0000FF' : '#00FF00'),
          });

        })
      }



    });
    this.layers = [track];
    this.bounds = track.getBounds();
    this.loading = false;
  }

}
