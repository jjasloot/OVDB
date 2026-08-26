import { Injectable, inject } from "@angular/core";
import { Layer, tileLayer } from "leaflet";
import { maplibreGL } from "@maplibre/maplibre-gl-leaflet";
import { setWorkerUrl, type Map as MaplibreMap } from "maplibre-gl";
import { ThemeService } from "./theme.service";

const OSM_URL = "https://{s}.tile.openstreetmap.org/{z}/{x}/{y}.png";
const OSM_ATTRIBUTION =
  '&copy; <a href="https://www.openstreetmap.org/copyright">OpenStreetMap</a> contributors';
const ESRI_URL =
  "https://server.arcgisonline.com/ArcGIS/rest/services/World_Topo_Map/MapServer/tile/{z}/{y}/{x}";
const ESRI_ATTRIBUTION =
  "Tiles &copy; Esri &mdash; Esri, DeLorme, NAVTEQ, TomTom, Intermap, iPC, USGS, FAO, NPS, NRCAN, GeoBase, Kadaster NL, Ordnance Survey, Esri Japan, METI, Esri China (Hong Kong), and the GIS User Community";

/**
 * OpenFreeMap's dark style. Vector tiles, served without an API key and with no
 * usage cap — which is why it replaced CARTO's dark_all: CARTO now stamps
 * "API KEY REQUIRED" across anonymous tiles. Credit for the tiles and the
 * underlying OSM data comes from the source's own TileJSON, which maplibre
 * renders in the attribution control it puts on the canvas.
 */
const OFM_DARK_STYLE = "https://tiles.openfreemap.org/styles/dark";

/** The top of the MapLibre zoom range, for zoom ranges that should not end. */
const MAX_STYLE_ZOOM = 24;

// One colour per kind of track, but deliberately a near-neutral set rather than
// seven distinct hues. Most maps here carry route lines on top, each in its own
// saturated colour chosen by the user, and a basemap that is itself colourful
// turns that into noise. So these separate mainly by lightness — heavy rail
// brightest, service track dimmest — with only enough hue to tell two kinds
// apart when you look for it, and not enough to read as colour at a glance.
//
// Heavy rail deliberately gets one colour for main and branch lines alike.
// OpenMapTiles does not carry OSM's `usage` tag at all — the properties on a
// track are access, bicycle, brunnel, class, foot, horse, indoor, layer, level,
// mtb_scale, network, official, oneway, ramp, service, subclass and surface —
// so the two are indistinguishable here, and a map about rail travel wants both
// equally visible anyway. What can be told apart is `service`, which marks
// yards, sidings, crossovers and spurs; that is track you look at rather than
// travel on, so it stays dim.
const RAIL_MAIN_COLOR = "rgb(112,126,144)";          // main and branch line: the brightest
const RAIL_NARROW_GAUGE_COLOR = "rgb(122,118,106)";  // a touch warm
const RAIL_FUNICULAR_COLOR = "rgb(124,112,118)";     // a touch rose: funicular and rack
// Tram, subway and monorail are only in the tiles from z14 at all, so there is
// no low-zoom clutter to weigh against: they can be as light as light rail
// without ever crowding a country-wide view, and they need it, since they
// arrive abruptly at a zoom where the map is already busy with streets.
const TRANSIT_LIGHT_RAIL_COLOR = "rgb(100,110,126)";
const TRANSIT_SUBWAY_COLOR = "rgb(100,110,132)";
const TRANSIT_MONORAIL_COLOR = "rgb(106,102,120)";
const TRANSIT_TRAM_COLOR = "rgb(102,116,116)";
const RAIL_TRANSIT_COLOR = "rgb(78,88,104)";         // any other transit subclass
const RAIL_MINOR_COLOR = "rgb(66,74,86)";            // yards, sidings, crossovers, spurs
const AERIALWAY_COLOR = "rgb(92,86,98)";
const FERRY_COLOR = "rgb(72,88,104)";

// From which zoom each kind of track appears. Most of these are not a choice:
// OpenMapTiles only starts carrying the class at that zoom, so a lower number
// would draw nothing and only mislead whoever reads it next. First zoom each was
// actually found in the live tiles: rail z8, ferry z8 or below, narrow gauge
// z10, light rail z11, aerialway z12, and service track, funicular, tram and
// subway all z14. Ferry is the one held back by choice, to keep a
// continent-wide view from filling up with sea crossings.
//
// Tram and subway in particular cannot be brought forward from here. Sampling
// 54 tiles per zoom across nine tram and metro cities found light rail at z11,
// z12 and z13 but not one tram or subway until z14. An OpenRailwayMap overlay
// draws both from z11 and is the way to have them earlier.
const RAIL_MAIN_MIN_ZOOM = 8;
const RAIL_TRANSIT_MIN_ZOOM = 11;
const RAIL_MINOR_MIN_ZOOM = 14;
const AERIALWAY_MIN_ZOOM = 12;
const FERRY_MIN_ZOOM = 7;
/** Sleeper hatching: only once the line beneath it is wide enough to carry it. */
const DASHLINE_MIN_ZOOM = 14;

/**
 * The style's first label layer. New track layers go in just under it, so they
 * draw over the roads and water but never over place names.
 */
const FIRST_LABEL_LAYER = "highway_name_other";

export const DARK_LAYER_NAME = "Dark";
export const DEFAULT_LIGHT_LAYER_NAME = "OpenStreetMap Mat";

/** Marks the fallback dark tiles for the CSS filter in `styles.scss`. */
const INVERTED_TILES_CLASS = "ovdb-inverted-tiles";

/**
 * MapLibre parses vector tiles in a module worker that it loads as a separate
 * file, and it works out where that file is from its own `import.meta.url`. The
 * build inlines MapLibre into an application chunk, so that guess resolves to a
 * path next to the chunk, where nothing is served — the worker 404s and the map
 * silently renders no tiles at all. Both worker files are copied to
 * `assets/maplibre` by the `assets` entry in `angular.json` instead, and pointed
 * at explicitly here. `maplibre-gl-worker.mjs` imports
 * `./maplibre-gl-shared.mjs`, so the two have to stay in the same folder.
 */
let workerUrlSet = false;
function ensureWorkerUrl(): void {
  if (!workerUrlSet) {
    setWorkerUrl(new URL("assets/maplibre/maplibre-gl-worker.mjs", document.baseURI).href);
    workerUrlSet = true;
  }
}

/**
 * Whether this browser can run the vector basemap: MapLibre needs WebGL 2.
 * Asked once and remembered — a device that cannot give us a context now will
 * not grow one later, and throwaway contexts are not free.
 */
let webgl2Support: boolean | null = null;
function supportsWebgl2(): boolean {
  if (webgl2Support === null) {
    try {
      webgl2Support = !!document.createElement("canvas").getContext("webgl2");
    } catch {
      // Locked-down browsers throw here rather than returning null.
      webgl2Support = false;
    }
  }
  return webgl2Support;
}

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

  /**
   * Dark tiles for browsers without WebGL 2, which cannot run the vector
   * basemap at all — without this they would get a blank canvas where the
   * basemap should be. The ordinary raster tiles, inverted in CSS: worse
   * cartography than OpenFreeMap's, since inverted water and woodland come out
   * off-hue, but it keeps dark mode dark and needs nothing the light layers do
   * not already need.
   */
  private invertedLightTiles(opacity: number): Layer {
    return tileLayer(OSM_URL, {
      opacity,
      attribution: OSM_ATTRIBUTION,
      className: INVERTED_TILES_CLASS,
    });
  }

  /**
   * The dark basemap, rendered from vector tiles. Building the layer is cheap:
   * the plugin only creates the WebGL map once the layer is added to a Leaflet
   * map, so the dark layer every `createBaseLayers()` call hands out costs
   * nothing until somebody actually selects it.
   */
  private darkTiles(opacity: number): Layer {
    if (!supportsWebgl2()) {
      return this.invertedLightTiles(opacity);
    }

    ensureWorkerUrl();
    const layer = maplibreGL({ style: OFM_DARK_STYLE });
    // Every add, not just the first: taking the layer off the map destroys the
    // WebGL map, and putting it back builds a fresh one. Switching to another
    // base layer and back therefore lands on a map that has had neither the
    // fading nor the track styling applied.
    layer.on("add", () => {
      // Leaflet's `opacity` option means nothing to a WebGL canvas. Fading the
      // canvas rather than the whole container leaves maplibre's attribution
      // control, a sibling of the canvas, at full contrast.
      layer.getCanvas().style.opacity = String(opacity);
      emphasiseTracks(layer.getMaplibreMap());
    });
    return layer;
  }
}

/**
 * Brings every kind of track in the dark basemap forward.
 *
 * OpenFreeMap's dark style paints railways in rgb(35,35,35) on a rgb(12,12,12)
 * background and hides them below z13 — z16 for yards, sidings and transit —
 * later than the tiles carry them, so they are all but invisible. It draws no
 * aerialways or ferry routes at any zoom, though the vector tiles carry both,
 * and OVDB counts cable cars, rack railways and boats among the things you can
 * travel on. So the six railway layers are restyled and two more are added.
 *
 * Patched on the live map instead of in a forked copy of the style, so
 * OpenFreeMap's own cartography updates keep coming through. Widths stay thin
 * on purpose — this is a backdrop for the route lines and station markers drawn
 * over it, and a network at full width reads as spaghetti.
 */
function emphasiseTracks(map: MaplibreMap): void {
  const apply = (): void => {
    /**
     * Zoom range for one of the style's own layers. A layer OpenFreeMap has
     * renamed or dropped is skipped, and says so in the return value: losing
     * the emphasis is a cosmetic regression, not something to throw over.
     * Colours and widths are set by the caller, inline, because only a literal
     * expression there type-checks against the paint property.
     */
    const tune = (id: string, minZoom: number): boolean => {
      if (!map.getLayer(id)) {
        return false;
      }
      map.setLayerZoomRange(id, minZoom, MAX_STYLE_ZOOM);
      return true;
    };

    // `railway` is class=rail without a service tag — every running heavy-rail
    // line, main and branch alike, plus the narrow gauge and funicular that
    // share the class. The last entry of a match is its fallback, so a subclass
    // that turns up later still draws, in the main-line colour.
    if (tune("railway", RAIL_MAIN_MIN_ZOOM)) {
      map.setPaintProperty("railway", "line-color", [
        "match", ["get", "subclass"],
        "narrow_gauge", RAIL_NARROW_GAUGE_COLOR,
        "funicular", RAIL_FUNICULAR_COLOR,
        RAIL_MAIN_COLOR,
      ]);
      map.setPaintProperty("railway", "line-width", [
        "interpolate", ["exponential", 1.3], ["zoom"],
        8, 0.9, 10, 1.3, 12, 2, 16, 3.5, 20, 7,
      ]);
    }

    // `railway_transit` is class=transit above ground — the style leaves the
    // tunnelled parts of a metro undrawn, which is left alone: a subway drawn
    // solid through the middle of a city reads as surface track.
    if (tune("railway_transit", RAIL_TRANSIT_MIN_ZOOM)) {
      map.setPaintProperty("railway_transit", "line-color", [
        "match", ["get", "subclass"],
        "subway", TRANSIT_SUBWAY_COLOR,
        "tram", TRANSIT_TRAM_COLOR,
        "light_rail", TRANSIT_LIGHT_RAIL_COLOR,
        "monorail", TRANSIT_MONORAIL_COLOR,
        RAIL_TRANSIT_COLOR,
      ]);
      // The z14 stop is deliberately a step up rather than a point on a smooth
      // ramp from z11: until z14 this layer is drawing light rail alone, and
      // tram and subway appear all at once at z14 and should arrive readable.
      map.setPaintProperty("railway_transit", "line-width", [
        "interpolate", ["exponential", 1.3], ["zoom"], 11, 0.8, 14, 1.8, 16, 2.6, 20, 5,
      ]);
    }

    // Yards, sidings, crossovers and spurs, of any class. One dim colour: the
    // point of drawing them at all is context around a station, not detail to
    // be read line by line.
    if (tune("railway_minor", RAIL_MINOR_MIN_ZOOM)) {
      map.setPaintProperty("railway_minor", "line-color", RAIL_MINOR_COLOR);
      map.setPaintProperty("railway_minor", "line-width", [
        "interpolate", ["exponential", 1.3], ["zoom"], 14, 0.8, 16, 2.5, 20, 5,
      ]);
    }

    // The dashlines draw the sleeper hatching over the line beneath them, in the
    // background colour, so they only start once that line is wide enough to
    // carry it — dashing a hairline turns it into a dotted smear.
    for (const dashline of ["railway_dashline", "railway_minor_dashline", "railway_transit_dashline"]) {
      if (map.getLayer(dashline)) {
        map.setLayerZoomRange(dashline, DASHLINE_MIN_ZOOM, MAX_STYLE_ZOOM);
      }
    }

    addTrackLayer(map, {
      id: "ovdb_aerialway",
      class: "aerialway",
      color: AERIALWAY_COLOR,
      minZoom: AERIALWAY_MIN_ZOOM,
      // Cable cars are short: a dash reads as one at any width they get.
      dash: [3, 2],
    });
    addTrackLayer(map, {
      id: "ovdb_ferry",
      class: "ferry",
      color: FERRY_COLOR,
      minZoom: FERRY_MIN_ZOOM,
      // Long dashes, the usual convention for a route over water.
      dash: [5, 3],
    });
  };

  if (map.isStyleLoaded()) {
    apply();
  } else {
    map.once("style.load", apply);
  }
}

/**
 * Adds a line layer for one `transportation` class the style leaves out. Placed
 * under the first label layer so it sits over roads and water but never over
 * place names, and skipped if it is somehow already there — `emphasiseTracks`
 * runs again on every re-add, and MapLibre rejects a duplicate layer id.
 */
function addTrackLayer(
  map: MaplibreMap,
  track: { id: string; class: string; color: string; minZoom: number; dash: [number, number] },
): void {
  if (map.getLayer(track.id)) {
    return;
  }
  map.addLayer(
    {
      id: track.id,
      type: "line",
      source: "openmaptiles",
      "source-layer": "transportation",
      minzoom: track.minZoom,
      filter: ["all",
        ["match", ["geometry-type"], ["LineString", "MultiLineString"], true, false],
        ["==", ["get", "class"], track.class],
      ],
      layout: { "line-join": "round", "line-cap": "round" },
      paint: {
        "line-color": track.color,
        "line-dasharray": track.dash,
        "line-width": [
          "interpolate", ["exponential", 1.3], ["zoom"],
          track.minZoom, 0.7, 14, 1.6, 20, 3.5,
        ],
      },
    },
    map.getLayer(FIRST_LABEL_LAYER) ? FIRST_LABEL_LAYER : undefined,
  );
}
