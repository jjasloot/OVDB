import { Component, OnInit, viewChild, inject, ElementRef, OnDestroy } from '@angular/core';
import { LatLngBounds, LatLng, geoJSON } from 'leaflet';
import { tileLayer } from 'leaflet';
import { TranslateService, TranslateModule } from '@ngx-translate/core';
import { TranslationService } from '../services/translation.service';
import { ApiService } from '../services/api.service';
import { ActivatedRoute } from '@angular/router';
import { LeafletModule } from '@bluehalo/ngx-leaflet';
import { NgClass } from '@angular/common';
import { MatProgressSpinner } from '@angular/material/progress-spinner';
import { MapProviderService } from '../services/map-provider.service';
import { MapConfigService } from '../services/map-config.service';
import * as maplibregl from 'maplibre-gl';

@Component({
    selector: 'app-single-route-map',
    templateUrl: './single-route-map.component.html',
    styleUrls: ['./single-route-map.component.scss'],
    imports: [LeafletModule, NgClass, MatProgressSpinner, TranslateModule]
})
export class SingleRouteMapComponent implements OnInit, OnDestroy {
  private translateService = inject(TranslateService);
  private translationService = inject(TranslationService);
  private activatedRoute = inject(ActivatedRoute);
  private apiService = inject(ApiService);
  private mapProviderService = inject(MapProviderService);
  private mapConfigService = inject(MapConfigService);

  readonly mapContainer = viewChild<HTMLElement>('mapContainer');
  readonly maplibreContainer = viewChild<ElementRef<HTMLDivElement>>('maplibreContainer');
  
  mapProvider = this.mapProviderService.currentProvider;
  private maplibreMap: maplibregl.Map | null = null;
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
      
      // Also update MapLibre if it's the active provider
      if (this.mapProvider() === 'maplibre') {
        setTimeout(() => this.showRouteOnMapLibre(), 100);
      }
    }
    catch {
      this.error = true;
    }
  }

  ngOnDestroy() {
    if (this.maplibreMap) {
      this.maplibreMap.remove();
      this.maplibreMap = null;
    }
  }

  private initMapLibre() {
    const container = this.maplibreContainer();
    if (!container) {
      return;
    }

    const style = this.mapConfigService.getMapLibreStyle('OpenStreetMap Mat');
    
    this.maplibreMap = new maplibregl.Map({
      container: container.nativeElement,
      style: style,
      center: [5.5, 52.0],
      zoom: 7,
      transformRequest: this.mapConfigService.getMapLibreTransformRequest()
    });

    this.maplibreMap.on('load', () => {
      this.maplibreMap!.addControl(new maplibregl.NavigationControl(), 'top-right');
    });
  }

  private showRouteOnMapLibre() {
    if (!this.maplibreMap || this.layers.length === 0) {
      return;
    }

    const leafletLayer = this.layers[0];
    const geojsonData = (leafletLayer as any).toGeoJSON();

    if (!this.maplibreMap.getSource('route')) {
      this.maplibreMap.addSource('route', {
        type: 'geojson',
        data: geojsonData
      });

      this.maplibreMap.addLayer({
        id: 'route-layer',
        type: 'line',
        source: 'route',
        paint: {
          'line-color': ['get', 'stroke'],
          'line-width': 3
        }
      });

      this.maplibreMap.on('click', 'route-layer', (e) => {
        if (e.features && e.features.length > 0) {
          const feature = e.features[0];
          const properties = feature.properties as any;
          
          let popup = '<h2>' + properties.name + '</h2><p>';
          popup += this.translateService.instant('MAP.POPUP.TYPE') + ': ' + properties.type;
          if (properties.description) {
            popup += '<br>' + this.translateService.instant('MAP.POPUP.REMARK') + ': ' + properties.description;
          }
          if (properties.lineNumber) {
            popup += '<br>' + this.translateService.instant('MAP.POPUP.LINENUMBER') + ': ' + properties.lineNumber;
          }
          if (properties.operatingCompany) {
            popup += '<br>' + this.translateService.instant('MAP.POPUP.OPERATINGCOMPANY') + ': ' + properties.operatingCompany;
          }
          popup += '</p>';

          new maplibregl.Popup()
            .setLngLat(e.lngLat)
            .setHTML(popup)
            .addTo(this.maplibreMap!);
        }
      });

      this.maplibreMap.on('mouseenter', 'route-layer', () => {
        this.maplibreMap!.getCanvas().style.cursor = 'pointer';
      });
      this.maplibreMap.on('mouseleave', 'route-layer', () => {
        this.maplibreMap!.getCanvas().style.cursor = '';
      });
    } else {
      const source = this.maplibreMap.getSource('route') as maplibregl.GeoJSONSource;
      if (source) {
        source.setData(geojsonData);
      }
    }

    if (this.bounds && this.bounds.isValid()) {
      const sw = this.bounds.getSouthWest();
      const ne = this.bounds.getNorthEast();
      this.maplibreMap.fitBounds([
        [sw.lng, sw.lat],
        [ne.lng, ne.lat]
      ], { padding: 50 });
    }
  }

}
