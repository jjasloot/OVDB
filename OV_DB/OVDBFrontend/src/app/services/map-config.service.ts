import { Injectable } from '@angular/core';
import { tileLayer, TileLayer } from 'leaflet';

export interface MapLayerConfig {
  name: string;
  url: string;
  attribution: string;
  opacity: number;
}

export interface MapLibreStyleConfig {
  name: string;
  style: any;
}

@Injectable({
  providedIn: 'root'
})
export class MapConfigService {
  // Layer configurations that work for both Leaflet and MapLibre
  private layerConfigs: MapLayerConfig[] = [
    {
      name: 'OpenStreetMap',
      url: 'https://{s}.tile.openstreetmap.org/{z}/{x}/{y}.png',
      attribution: '&copy; <a href="https://www.openstreetmap.org/copyright">OpenStreetMap</a> contributors',
      opacity: 0.8
    },
    {
      name: 'OpenStreetMap Mat',
      url: 'https://{s}.tile.openstreetmap.org/{z}/{x}/{y}.png',
      attribution: '&copy; <a href="https://www.openstreetmap.org/copyright">OpenStreetMap</a> contributors',
      opacity: 0.5
    },
    {
      name: 'Esri WorldTopoMap',
      url: 'https://server.arcgisonline.com/ArcGIS/rest/services/World_Topo_Map/MapServer/tile/{z}/{y}/{x}',
      attribution: 'Tiles &copy; Esri &mdash; Esri, DeLorme, NAVTEQ, TomTom, Intermap, iPC, USGS, FAO, NPS, NRCAN, GeoBase, Kadaster NL, Ordnance Survey, Esri Japan, METI, Esri China (Hong Kong), and the GIS User Community',
      opacity: 0.65
    }
  ];

  // Get layer configurations
  getLayerConfigs(): MapLayerConfig[] {
    return this.layerConfigs;
  }

  // Get Leaflet tile layers
  getLeafletLayers(): { [key: string]: TileLayer } {
    const layers: { [key: string]: TileLayer } = {};
    this.layerConfigs.forEach(config => {
      layers[config.name] = tileLayer(config.url, {
        opacity: config.opacity,
        attribution: config.attribution
      });
    });
    return layers;
  }

  // Get all available MapLibre styles
  getMapLibreStyles(): MapLibreStyleConfig[] {
    return this.layerConfigs.map(config => ({
      name: config.name,
      style: this.createMapLibreStyle(config)
    }));
  }

  // Get MapLibre style for a specific layer
  getMapLibreStyle(layerName: string = 'OpenStreetMap Mat'): any {
    const config = this.layerConfigs.find(l => l.name === layerName) || this.layerConfigs[1];
    return this.createMapLibreStyle(config);
  }

  private createMapLibreStyle(config: MapLayerConfig): any {
    return {
      version: 8,
      sources: {
        'osm-tiles': {
          type: 'raster',
          tiles: [config.url.replace('{s}', 'a')],
          tileSize: 256,
          attribution: config.attribution
        }
      },
      layers: [
        {
          id: 'osm-tiles-layer',
          type: 'raster',
          source: 'osm-tiles',
          paint: {
            'raster-opacity': config.opacity
          }
        }
      ]
    };
  }

  // Get MapLibre transform request to set Referrer header for OSM
  getMapLibreTransformRequest() {
    return (url: string, resourceType: string) => {
      if (url.includes('openstreetmap.org')) {
        return {
          url: url,
          headers: { 'Referer': window.location.origin }
        };
      }
      return { url: url };
    };
  }
}
