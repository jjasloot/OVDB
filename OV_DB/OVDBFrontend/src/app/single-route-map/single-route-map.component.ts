import { Component, OnInit, viewChild } from '@angular/core';
import { LatLngBounds, LatLng, geoJSON } from 'leaflet';
import { tileLayer } from 'leaflet';
import { TranslateService, TranslateModule } from '@ngx-translate/core';
import { TranslationService } from '../services/translation.service';
import { ApiService } from '../services/api.service';
import { ActivatedRoute } from '@angular/router';
import { LeafletModule } from '@bluehalo/ngx-leaflet';
import { NgClass } from '@angular/common';
import { MatProgressSpinner } from '@angular/material/progress-spinner';

@Component({
    selector: 'app-single-route-map',
    templateUrl: './single-route-map.component.html',
    styleUrls: ['./single-route-map.component.scss'],
    imports: [LeafletModule, NgClass, MatProgressSpinner, TranslateModule]
})
export class SingleRouteMapComponent implements OnInit {
  readonly mapContainer = viewChild<HTMLElement>('mapContainer');
  loading = false;
  layers = [];
  error: boolean;
  active: string;
  guid: string;
  routeId: number;
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
  // tslint:disable-next-line: variable-name
  private _bounds: LatLngBounds;

  get mapHeight() {
    const mapContainer = this.mapContainer();
    if (mapContainer) {
      return mapContainer.offsetHeight;
    }
    return 500;
  }

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



  constructor(
    private translateService: TranslateService,
    private translationService: TranslationService,
    private activatedRoute: ActivatedRoute,
    private apiService: ApiService) { }




  ngOnInit() {
    this.activatedRoute.paramMap.subscribe(p => {
      this.routeId = +p.get('routeId');
      this.guid = p.get('guid');
      this.getRoute();
    });
    this.translationService.languageChanged.subscribe(() => this.getRoute());
  }



  private async getRoute() {
    try {
      this.loading = true;

      const text = await this.apiService.getSingleRoute(this.routeId, this.guid, this.translationService.language).toPromise();
      const parent = this;
      const track = geoJSON(text as any, {
        style: feature => {
          return {
            color: feature.properties.stroke,
            weight: 3
          };
        },
        onEachFeature(feature, layer) {
          let popup = '<h2>' + feature.properties.name + '</h2><p>'
            + parent.translateService.instant('MAP.POPUP.TYPE')
            + ': ' + feature.properties.type;
          if (!!feature.properties.description) {
            popup += '<br>' + parent.translateService.instant('MAP.POPUP.REMARK') + ': ' + feature.properties.description;
          }
          if (!!feature.properties.lineNumber) {
            popup += '<br>' + parent.translateService.instant('MAP.POPUP.LINENUMBER') + ': ' + feature.properties.lineNumber;
          }
          if (!!feature.properties.operatingCompany) {
            popup += '<br>' + parent.translateService.instant('MAP.POPUP.OPERATINGCOMPANY') + ': ' + feature.properties.operatingCompany;
          }
          popup += '</p>';
          layer.on('click', f => {
            f.target.setStyle({ weight: 8, });
            f.target.bringToFront();
            f.target.getPopup().on('remove', () => {
              f.target.setStyle({
                weight: 3,
              });
            });
          });
          layer.bindPopup(popup);
        }
      });
      this.layers = [track];
      this.bounds = track.getBounds();
      this.loading = false;
    }
    catch {
      this.error = true;
    }
  }

}
