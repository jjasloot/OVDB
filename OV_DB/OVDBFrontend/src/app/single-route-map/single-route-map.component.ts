import { Component, OnInit, viewChild, inject, ChangeDetectionStrategy } from '@angular/core';
import { LatLngBounds, LatLng, geoJSON, Layer } from 'leaflet';
import { TranslateService, TranslateModule } from '@ngx-translate/core';
import { TranslationService } from '../services/translation.service';
import { ApiService } from '../services/api.service';
import { MapTileLayersService } from '../services/map-tile-layers.service';
import { ActivatedRoute } from '@angular/router';
import { LeafletModule } from '@bluehalo/ngx-leaflet';
import { NgClass } from '@angular/common';
import { MatProgressSpinner } from '@angular/material/progress-spinner';

@Component({
    selector: 'app-single-route-map',
    templateUrl: './single-route-map.component.html',
    styleUrls: ['./single-route-map.component.scss'],
    changeDetection: ChangeDetectionStrategy.Eager,
    imports: [LeafletModule, NgClass, MatProgressSpinner, TranslateModule]
})
export class SingleRouteMapComponent implements OnInit {
  private translateService = inject(TranslateService);
  private translationService = inject(TranslationService);
  private activatedRoute = inject(ActivatedRoute);
  private apiService = inject(ApiService);
  private mapTileLayersService = inject(MapTileLayersService);

  readonly mapContainer = viewChild<HTMLElement>('mapContainer');
  loading = false;
  layers: Layer[] = [];
  error = false;
  active = '';
  guid!: string;
  routeId!: number;
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
   private _bounds!: LatLngBounds;

  get mapHeight() {
    const mapContainer = this.mapContainer();
    if (mapContainer) {
      return mapContainer.offsetHeight;
    }
    return 500;
  }

  baseLayers = this.mapTileLayersService.createBaseLayers();

  options = {
    layers: [this.mapTileLayersService.defaultLayer(this.baseLayers)],
    zoom: 5
  };
  leafletLayersControl = {
    baseLayers: this.baseLayers,
    overlays: {},
  };




  ngOnInit() {
    this.activatedRoute.paramMap.subscribe(p => {
      this.routeId = +p.get('routeId')!;
      this.guid = p.get('guid')!;
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
            color: feature!.properties.stroke,
            weight: 3
          };
        },
        onEachFeature(feature, layer) {
          let popup = '<h2>' + feature.properties.name + '</h2><p>'
            + parent.translateService.instant('MAP.POPUP.TYPE')
            + ': ' + feature.properties.type;
          if (feature.properties.description) {
            popup += '<br>' + parent.translateService.instant('MAP.POPUP.REMARK') + ': ' + feature.properties.description;
          }
          if (feature.properties.lineNumber) {
            popup += '<br>' + parent.translateService.instant('MAP.POPUP.LINENUMBER') + ': ' + feature.properties.lineNumber;
          }
          if (feature.properties.operatingCompany) {
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
