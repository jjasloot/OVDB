import { Injectable, inject } from "@angular/core";
import { Layer, tileLayer } from "leaflet";
import { ThemeService } from "./theme.service";

const OSM_URL = "https://{s}.tile.openstreetmap.org/{z}/{x}/{y}.png";
const CARTO_DARK_URL = "https://{s}.basemaps.cartocdn.com/dark_all/{z}/{x}/{y}{r}.png";
const OSM_ATTRIBUTION =
  '&copy; <a href="https://www.openstreetmap.org/copyright">OpenStreetMap</a> contributors';
const CARTO_ATTRIBUTION =
  `${OSM_ATTRIBUTION} &copy; <a href="https://carto.com/attributions">CARTO</a>`;
const ESRI_URL =
  "https://server.arcgisonline.com/ArcGIS/rest/services/World_Topo_Map/MapServer/tile/{z}/{y}/{x}";
const ESRI_ATTRIBUTION =
  "Tiles &copy; Esri &mdash; Esri, DeLorme, NAVTEQ, TomTom, Intermap, iPC, USGS, FAO, NPS, NRCAN, GeoBase, Kadaster NL, Ordnance Survey, Esri Japan, METI, Esri China (Hong Kong), and the GIS User Community";

export const DARK_LAYER_NAME = "Dark";
export const DEFAULT_LIGHT_LAYER_NAME = "OpenStreetMap Mat";

/**
 * Base layers for the Leaflet maps. Every call builds fresh layer instances —
 * a Leaflet layer belongs to one map at a time, so maps shown side by side
 * cannot share them.
 */
@Injectable({ providedIn: "root" })
export class MapTileLayersService {
  private themeService = inject(ThemeService);

  createBaseLayers(): { [name: string]: Layer } {
    return {
      OpenStreetMap: this.lightTiles(0.8),
      // Faded so route lines and markers stay readable on top.
      [DEFAULT_LIGHT_LAYER_NAME]: this.lightTiles(0.5),
      [DARK_LAYER_NAME]: this.darkTiles(0.8),
      "Esri WorldTopoMap": tileLayer(ESRI_URL, {
        opacity: 0.65,
        attribution: ESRI_ATTRIBUTION,
      }),
    };
  }

  /**
   * Which of the base layers to show first: dark tiles when the app is in dark
   * mode. Applied when the map is created; switching theme afterwards leaves
   * the tiles alone so a manual choice in the layer control is not overridden.
   */
  defaultLayerName(): string {
    return this.themeService.isDarkMode ? DARK_LAYER_NAME : DEFAULT_LIGHT_LAYER_NAME;
  }

  defaultLayer(baseLayers: { [name: string]: Layer }): Layer {
    return baseLayers[this.defaultLayerName()];
  }

  /** Single themed layer, for maps without a layer control. */
  createThemedLayer(opacity = 0.5): Layer {
    return this.themeService.isDarkMode ? this.darkTiles(opacity) : this.lightTiles(opacity);
  }

  private lightTiles(opacity: number): Layer {
    return tileLayer(OSM_URL, { opacity, attribution: OSM_ATTRIBUTION });
  }

  private darkTiles(opacity: number): Layer {
    return tileLayer(CARTO_DARK_URL, {
      opacity,
      subdomains: "abcd",
      maxZoom: 20,
      attribution: CARTO_ATTRIBUTION,
    });
  }
}
