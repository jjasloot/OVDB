import { Component, OnInit, AfterViewInit, ChangeDetectorRef, NgZone, EventEmitter, OnDestroy, input, viewChild, signal, inject, ElementRef } from "@angular/core";
import moment from "moment";
import { tileLayer } from "leaflet";
import { ApiService } from "../services/api.service";
import { LatLngBounds, LatLng, geoJSON, LatLngLiteral } from "leaflet";
import { FilterSettings } from "../models/filterSettings";
import { MatDialog } from "@angular/material/dialog";
import { Country } from "../models/country.model";
import { MapFilterComponent } from "../map-filter/map-filter.component";
import { TranslateService, TranslateModule } from "@ngx-translate/core";
import { TranslationService } from "../services/translation.service";
import { Router, ActivatedRoute } from "@angular/router";
import { MapInstanceDialogComponent } from "../map-instance-dialog/map-instance-dialog.component";
import { switchMap } from "rxjs/operators";
import { Observable } from "rxjs";
import { MapDataDTO } from "../models/map-data.model";
import { v4 as uuidv4 } from "uuid";
import { SignalRService } from "../services/signal-r.service";
import { MapProviderService } from "../services/map-provider.service";
import { MapConfigService } from "../services/map-config.service";
import * as maplibregl from 'maplibre-gl';
import {
  NgTemplateOutlet,
  NgClass,
  UpperCasePipe,
  KeyValuePipe,
} from "@angular/common";
import {
  MatExpansionPanel,
  MatExpansionPanelHeader,
  MatExpansionPanelTitle,
} from "@angular/material/expansion";
import { LeafletModule } from "@bluehalo/ngx-leaflet";
import { MatProgressSpinner } from "@angular/material/progress-spinner";
import { MatButton } from "@angular/material/button";
import { MatIcon } from "@angular/material/icon";

@Component({
  selector: "app-map",
  templateUrl: "./map.component.html",
  styleUrls: ["./map.component.scss"],
  imports: [
    NgTemplateOutlet,
    MatExpansionPanel,
    MatExpansionPanelHeader,
    MatExpansionPanelTitle,
    LeafletModule,
    NgClass,
    MatProgressSpinner,
    MatButton,
    MatIcon,
    UpperCasePipe,
    KeyValuePipe,
    TranslateModule,
  ],
})
export class MapComponent implements OnInit, AfterViewInit, OnDestroy {
  private translateService = inject(TranslateService);
  private translationService = inject(TranslationService);
  private apiService = inject(ApiService);
  private dialog = inject(MatDialog);
  private router = inject(Router);
  private activatedRoute = inject(ActivatedRoute);
  private signalRService = inject(SignalRService);
  private cd = inject(ChangeDetectorRef);
  private mapProviderService = inject(MapProviderService);
  private mapConfigService = inject(MapConfigService);

  readonly guid = input<string>(undefined);
  readonly mapContainer = viewChild<HTMLElement>("mapContainer");
  readonly maplibreContainer = viewChild<ElementRef<HTMLDivElement>>("maplibreContainer");
  
  mapProvider = this.mapProviderService.currentProvider;
  private maplibreMap: maplibregl.Map | null = null;
  private maplibreGeoJsonSource: maplibregl.GeoJSONSource | null = null;
  loading: boolean | number = false;
  from: moment.Moment;
  to: moment.Moment;
  selectedRegion: number[] = [];
  selectedTypes: number[] = [];
  layers = [];
  countries: Country[];
  selectedYears: number[];
  error: boolean;
  active = signal<string>("");
  selectedRoute;
  includeLineColours = true;
  requestIdentifier?: string;
  limitToSelectedArea = false;
  get bounds(): LatLngBounds {
    return this._bounds;
  }
  set bounds(value: LatLngBounds) {
    if (!!value && value.isValid()) {
      this._bounds = value;
    } else {
      this.bounds = new LatLngBounds(
        new LatLng(50.656245, 2.92136),
        new LatLng(53.604563, 7.428211)
      );
    }
  }
   private _bounds: LatLngBounds;

  defaults = new Map<string, FilterSettings>([
    [
      "ThisMonth",
      new FilterSettings(
        "ThisMonth",
        true,
        false,
        moment().startOf("month"),
        moment().startOf("month").add(1, "month"),
        []
      ),
    ],
    [
      "ThisYear",
      new FilterSettings(
        "ThisYear",
        true,
        false,
        null,
        null,
        [],
        [],
        [moment().year()]
      ),
    ],
    [
      "LastMonth",
      new FilterSettings(
        "LastMonth",
        true,
        false,
        moment().startOf("month").subtract(1, "month"),
        moment().startOf("month"),
        []
      ),
    ],
    [
      "LastYear",
      new FilterSettings(
        "LastYear",
        true,
        false,
        null,
        null,
        [],
        [],
        [moment().year() - 1]
      ),
    ],
    ["All", new FilterSettings("All", true, false, null, null, [])],
  ]);

  get mapHeight() {
    const mapContainer = this.mapContainer();
    if (mapContainer) {
      return mapContainer.offsetHeight;
    }
    return 500;
  }

  baseLayers = {
    OpenStreetMap: tileLayer(
      "https://{s}.tile.openstreetmap.org/{z}/{x}/{y}.png",
      {
        opacity: 0.8,
        attribution:
          '&copy; <a href="https://www.openstreetmap.org/copyright">OpenStreetMap</a> contributors',
      }
    ),
    "OpenStreetMap Mat": tileLayer(
      "https://{s}.tile.openstreetmap.org/{z}/{x}/{y}.png",
      {
        opacity: 0.5,
        attribution:
          '&copy; <a href="https://www.openstreetmap.org/copyright">OpenStreetMap</a> contributors',
      }
    ),
    "Esri WorldTopoMap": tileLayer(
      "https://server.arcgisonline.com/ArcGIS/rest/services/World_Topo_Map/MapServer/tile/{z}/{y}/{x}",
      {
        opacity: 0.65,
               attribution:
          "Tiles &copy; Esri &mdash; Esri, DeLorme, NAVTEQ, TomTom, Intermap, iPC, USGS, FAO, NPS, NRCAN, GeoBase, Kadaster NL, Ordnance Survey, Esri Japan, METI, Esri China (Hong Kong), and the GIS User Community",
      }
    ),
  };

  options = {
    layers: [this.baseLayers["OpenStreetMap Mat"]],
    zoom: 5,
  };
  leafletLayersControl = {
    baseLayers: this.baseLayers,
    // overlays: this.layers
  };

  getRoutes$ = new EventEmitter<string>();

  constructor() {
       const ngZone = inject(NgZone);

       window["angularComponentRef"] = { component: this, zone: ngZone };
  }
  ngAfterViewInit(): void {
    this.cd.detectChanges();
    if (this.mapProvider() === 'maplibre') {
      this.initMapLibre();
    }
  }

  ngOnInit() {
    this.getRoutes$
      .pipe(
        switchMap((filter) => {
          return this.getRoutes(filter);
        })
      )
      .subscribe({
        next: (data: MapDataDTO) => {
          this.showRoutes(data);
        },
        error: () => {
          this.error = true;
        },
      });

    this.readFromQueryParams();
    this.translationService.languageChanged.subscribe(() =>
      this.getRoutes$.next(this.getFilter())
    );
    this.signalRService.connect();
    this.signalRService.updates$.subscribe({
      next: (data) => {
        if (data.requestIdentifier === this.requestIdentifier) {
          this.loading = data.percentage;
          this.cd.detectChanges();
        }
      },
    });
  }

  ngOnDestroy() {
    this.signalRService.disconnect();
    if (this.maplibreMap) {
      this.maplibreMap.remove();
      this.maplibreMap = null;
    }
  }

  readFromQueryParams() {
    const queryParams = this.activatedRoute.snapshot.queryParamMap;
    if (queryParams.keys.length === 0) {
      this.setOption(this.defaults.get("All"));
      return;
    }
    if (queryParams.has("from")) {
      this.from = moment(+queryParams.get("from"));
    }
    if (queryParams.has("from")) {
      this.to = moment(+queryParams.get("to"));
    }
    if (queryParams.has("types")) {
      this.selectedTypes = queryParams
        .get("types")
        .split(",")
        .map((c) => +c);
    }
    if (queryParams.has("countries")) {
      this.selectedRegion = queryParams
        .get("countries")
        .split(",")
        .map((c) => +c);
    }
    if (queryParams.has("years")) {
      this.selectedYears = queryParams
        .get("years")
        .split(",")
        .map((c) => +c);
    }
    this.includeLineColours = queryParams.has("includeLineColours");
    this.limitToSelectedArea = queryParams.has("limitToSelectedArea");
    this.active.set("filter");
    this.getRoutes$.next(this.getFilter());
    this.setApplicableFilter();
  }

  private getRoutes(filter: string): Observable<MapDataDTO> {
    //Generate a GUID
    this.requestIdentifier = uuidv4();

    return this.apiService.getRoutes(
      filter,
      this.guid(),
      this.translationService.language,
      this.includeLineColours,
      this.limitToSelectedArea,
      this.requestIdentifier
    );
  }
  private showRoutes(data: MapDataDTO) {
    const parent = this;
    const track = geoJSON(data.routes, {
      style: (feature) => {
        return {
          color: feature.properties.stroke,
          weight: 3,
        };
      },
      onEachFeature(feature, layer) {
        if (feature.properties.name) {
          let popup = "<h2>" + feature.properties.name + "</h2><p>";
          popup +=
            `<a href="javascript:void(0)" onclick="
          parent.angularComponentRef.zone.run(()=>
          parent.angularComponentRef.component.showDialog(` +
            feature.properties.id +
            `))">` +
            feature.properties.totalInstances +
            " " +
            parent.translateService.instant("INSTANCES") +
            `</a>`;

          popup +=
            "<br>" +
            parent.translateService.instant("MAP.POPUP.TYPE") +
            ": " +
            feature.properties.type;
          if (feature.properties.description) {
            popup +=
              "<br>" +
              parent.translateService.instant("MAP.POPUP.REMARK") +
              ": " +
              feature.properties.description;
          }
          if (feature.properties.lineNumber) {
            popup +=
              "<br>" +
              parent.translateService.instant("MAP.POPUP.LINENUMBER") +
              ": " +
              feature.properties.lineNumber;
          }
          if (feature.properties.operatingCompany) {
            popup +=
              "<br>" +
              parent.translateService.instant("MAP.POPUP.OPERATINGCOMPANY") +
              ": " +
              feature.properties.operatingCompany;
          }
          if (feature.properties.distance) {
            popup +=
              "<br>" +
              parent.translateService.instant("ROUTES.DISTANCE") +
              ": " +
              feature.properties.distance +
              " km";
          }
          if (feature.properties.owner) {
            popup +=
              `<br><a href="javascript:void(0)" onclick="
            parent.angularComponentRef.zone.run(()=>
            parent.angularComponentRef.component.edit(` +
              feature.properties.id +
              `))">` +
              parent.translateService.instant("EDIT") +
              `</a>`;
            popup +=
              `&nbsp;<a href="javascript:void(0)" onclick="
            parent.angularComponentRef.zone.run(()=>
            parent.angularComponentRef.component.editInstances(` +
              feature.properties.id +
              `))">` +
              parent.translateService.instant("INSTANCES.EDITINSTANCE") +
              `</a>`;
          }
          popup += "</p>";
          layer.bindPopup(popup);
        }
        if (feature.properties.o) {
          layer.on("click", (f) => {
            if (parent.selectedRoute) {
              parent.selectedRoute.setStyle({ weight: 3 });
            }
            parent.selectedRoute = f.target;
            f.target.setStyle({ weight: 8 });
            f.target.bringToFront();
            if (feature.properties.name) {
              f.target.getPopup().on("remove", () => {
                f.target.setStyle({
                  weight: 3,
                });
              });
            }
          });
        }
      },
    });
    this.layers = [track];
    if (!!data.area && !track.getBounds().isValid()) {
      this.bounds = new LatLngBounds(
        {
          lat: data.area.southEast.latitude,
          lng: data.area.southEast.longitude,
        } as LatLngLiteral,
        {
          lat: data.area.northWest.latitude,
          lng: data.area.northWest.longitude,
        } as LatLngLiteral
      );
    } else {
      this.bounds = track.getBounds();
    }
    this.loading = false;
    
    // Also update MapLibre if it's the active provider
    if (this.mapProvider() === 'maplibre') {
      setTimeout(() => this.showRoutesOnMapLibre(), 100);
    }
  }
  private getFilter() {
    const queryParams = {};
       if (!!this.to && !!this.from) {
      queryParams["to"] = this.to.valueOf();
      queryParams["from"] = this.from.valueOf();
    }
    if (this.selectedRegion && this.selectedRegion.length > 0) {
      queryParams["countries"] = this.selectedRegion.join(",");
    }
    if (this.selectedTypes && this.selectedTypes.length > 0) {
      queryParams["types"] = this.selectedTypes.join(",");
    }
    if (this.selectedYears && this.selectedYears.length > 0) {
      queryParams["years"] = this.selectedYears.join(",");
    }
    if (this.includeLineColours) {
      queryParams["includeLineColours"] = "true";
    }
    if (this.limitToSelectedArea) {
      queryParams["limitToSelectedArea"] = "true";
    }
       this.router.navigate(
      this.activatedRoute.snapshot.url.map((u) => u.path),
      { queryParams }
    );

    this.loading = true;
    let filter = "";
    if (!!this.to && !!this.from) {
      filter += filter +=
        "(Date ge " +
        this.from.format("YYYY-MM-DD") +
        " and Date lt " +
        this.to.format("YYYY-MM-DD") +
        ")  and ";
    }
    if (this.selectedRegion && this.selectedRegion.length > 0) {
      filter += "(";
      this.selectedRegion.forEach((option) => {
        filter += "Route/Regions/any(region: region/Id eq " + option + ") or ";
      });
      if (filter.endsWith(" or ")) {
        filter = filter.slice(0, filter.length - 4);
      }
      filter += ") and ";
    }
    if (this.selectedTypes && this.selectedTypes.length > 0) {
      filter += "(";
      this.selectedTypes.forEach((option) => {
        filter += "Route/RouteTypeId eq " + option + " or ";
      });
      if (filter.endsWith(" or ")) {
        filter = filter.slice(0, filter.length - 4);
      }
      filter += ") and ";
    }

    if (this.selectedYears && this.selectedYears.length > 0) {
      filter += "(";
      this.selectedYears.forEach((option) => {
        if (!option) {
          filter += "Route/FirstDateTime eq null or ";
        } else {
          const start = moment().year(option).startOf("year");
          const end = moment()
            .year(option + 1)
            .startOf("year");

          filter +=
            "(Date ge " +
            start.format("YYYY-MM-DD") +
            " and Date lt " +
            end.format("YYYY-MM-DD") +
            ") or ";
        }
      });
      if (filter.endsWith(" or ")) {
        filter = filter.slice(0, filter.length - 4);
      }
      filter += ") and ";
    }

    if (filter.endsWith(" and ")) {
      filter = filter.slice(0, filter.length - 5);
    }
    return filter;
  }

  setOption(option: FilterSettings) {
    this.from = option.from;
    this.to = option.to;
    this.selectedRegion = [...option.selectedCountries];
    this.selectedTypes = [...option.selectedTypes];
    this.selectedYears = [...option.selectedYears];
    this.includeLineColours = option.includeLineColours;
    this.limitToSelectedArea = option.limitToSelectedAreas;
    this.active.set(option.name);
    if (
      this.limitToSelectedArea &&
      this.selectedRegion.length > 0 &&
      !this.signalRService.connected
    ) {
      this.signalRService.connect();
    }
    if (!this.limitToSelectedArea && this.signalRService.connected) {
      this.signalRService.disconnect();
    }
    this.getRoutes$.next(this.getFilter());
    this.setApplicableFilter();
  }

  openDialog() {
    const settings = new FilterSettings(
      "",
      this.includeLineColours,
      this.limitToSelectedArea,
      this.from,
      this.to,
      this.selectedRegion,
      this.selectedTypes,
      this.selectedYears
    );
    const dialogRef = this.dialog.open(MapFilterComponent, {
      width: "75%",
      data: { settings, guid: this.guid() },
    });
    dialogRef.afterClosed().subscribe((result: FilterSettings) => {
      if (result) {
        result.name = "filter";
        this.setOption(result);
      }
    });
  }

  showDialog(id: number) {
    const limits = this.selectedYears.map((s) => {
      return {
        start: moment().year(s).startOf("year"),
        end: moment()
          .year(s + 1)
          .startOf("year"),
      };
    });
    if (!!this.from && !!this.to) {
      limits.push({
        start: moment(this.from),
        end: moment(this.to),
      });
    }
    const dialog = this.dialog.open(MapInstanceDialogComponent, {
      data: {
        id,
        limits,
        mapGuid: this.guid(),
      },
      width: "50%",
    });
  }

  edit(id: number) {
    this.router.navigate(["/", "admin", "routes", id]);
  }

  editInstances(id: number) {
    this.router.navigate(["/", "admin", "routes", "instances", id]);
  }

  refresh() {
    this.getRoutes$.next(this.getFilter());
  }

  setApplicableFilter() {
    const to = this.to;
    const from = this.from;
    const years = this.selectedYears;
    this.defaults.forEach((value, key) => {
      if (
        (value.from?.isSame(from) ?? (value.from == null && from == null)) &&
        (value.to?.isSame(to) ?? (value.to == null && from == null)) &&
        value.selectedYears.every((y) => years.includes(y)) &&
        (years??[]).every((y) => value.selectedYears.includes(y))
      ) {
        this.active.set(key);
      }
    });
  }

  private initMapLibre() {
    const container = this.maplibreContainer();
    if (!container) {
      console.error('MapLibre container not found');
      return;
    }

    const style = this.mapConfigService.getMapLibreStyle('OpenStreetMap Mat');
    
    this.maplibreMap = new maplibregl.Map({
      container: container.nativeElement,
      style: style,
      center: [5.5, 52.0], // Netherlands approximate center
      zoom: 7,
      transformRequest: this.mapConfigService.getMapLibreTransformRequest()
    });

    this.maplibreMap.on('load', () => {
      // Add navigation controls
      this.maplibreMap!.addControl(new maplibregl.NavigationControl(), 'top-right');
      
      // Add style switcher control
      this.addStyleSwitcher();
      
      // If routes are already loaded, show them now
      if (this.layers.length > 0) {
        this.showRoutesOnMapLibre();
      }
    });
  }

  private addStyleSwitcher() {
    if (!this.maplibreMap) return;

    const styles = this.mapConfigService.getMapLibreStyles();
    const parent = this;
    
    // Create style switcher control as a select dropdown
    class StyleSwitcherControl {
      private _map: maplibregl.Map | undefined;
      private _container: HTMLDivElement | undefined;
      private _select: HTMLSelectElement | undefined;
      private _styles: any[];
      private _parent: any;

      constructor(styles: any[], parentComponent: any) {
        this._styles = styles;
        this._parent = parentComponent;
      }

      onAdd(map: maplibregl.Map): HTMLElement {
        this._map = map;
        this._container = document.createElement('div');
        this._container.className = 'maplibregl-ctrl maplibregl-ctrl-group';
        this._container.style.backgroundColor = 'white';
        this._container.style.padding = '5px';
        
        // Create select dropdown
        this._select = document.createElement('select');
        this._select.style.border = 'none';
        this._select.style.fontSize = '12px';
        this._select.style.cursor = 'pointer';
        this._select.style.backgroundColor = 'white';
        
        // Add options
        this._styles.forEach((style, index) => {
          const option = document.createElement('option');
          option.value = index.toString();
          option.text = style.name;
          if (index === 1) { // OpenStreetMap Mat is default
            option.selected = true;
          }
          this._select!.appendChild(option);
        });
        
        // Handle style change
        this._select.onchange = () => {
          const selectedIndex = parseInt(this._select!.value);
          const newStyle = this._styles[selectedIndex].style;
          
          // Store current state
          const center = this._map!.getCenter();
          const zoom = this._map!.getZoom();
          const bearing = this._map!.getBearing();
          const pitch = this._map!.getPitch();
          
          // Change style
          this._map!.setStyle(newStyle);
          
          // Restore state and re-add routes after style loads
          this._map!.once('style.load', () => {
            this._map!.setCenter(center);
            this._map!.setZoom(zoom);
            this._map!.setBearing(bearing);
            this._map!.setPitch(pitch);
            
            // Re-add routes after style is loaded
            if (this._parent && this._parent.showRoutesOnMapLibre) {
              setTimeout(() => {
                this._parent.showRoutesOnMapLibre();
              }, 100);
            }
          });
        };
        
        this._container.appendChild(this._select);
        return this._container;
      }

      onRemove(): void {
        if (this._container && this._container.parentNode) {
          this._container.parentNode.removeChild(this._container);
        }
        this._map = undefined;
      }
    }

    const styleSwitcher = new StyleSwitcherControl(styles, parent);
    this.maplibreMap.addControl(styleSwitcher as any, 'top-right');
  }

  private showRoutesOnMapLibre() {
    if (!this.maplibreMap || this.layers.length === 0) {
      return;
    }

    // Ensure map is loaded before proceeding
    if (!this.maplibreMap.loaded()) {
      this.maplibreMap.once('load', () => {
        this.showRoutesOnMapLibre();
      });
      return;
    }

    // Ensure map style is loaded
    if (!this.maplibreMap.isStyleLoaded()) {
      this.maplibreMap.once('style.load', () => {
        this.showRoutesOnMapLibre();
      });
      return;
    }

    const leafletLayer = this.layers[0];
    const geojsonData = (leafletLayer as any).toGeoJSON();

    // Check if source exists, if not create it
    if (!this.maplibreMap.getSource('routes')) {
      this.maplibreMap.addSource('routes', {
        type: 'geojson',
        data: geojsonData
      });
    } else {
      // Update existing source
      const source = this.maplibreMap.getSource('routes') as maplibregl.GeoJSONSource;
      if (source) {
        source.setData(geojsonData);
      }
    }

    // Check if layer exists, if not create it
    if (!this.maplibreMap.getLayer('routes-layer')) {
      this.maplibreMap.addLayer({
        id: 'routes-layer',
        type: 'line',
        source: 'routes',
        paint: {
          'line-color': ['get', 'stroke'],
          'line-width': 3
        }
      });

      // Add popups on click - only add event listeners once
      this.maplibreMap.on('click', 'routes-layer', (e) => {
        if (e.features && e.features.length > 0) {
          const feature = e.features[0];
          const properties = feature.properties as any;
          
          // Create popup HTML similar to Leaflet version
          let popup = '<h2>' + properties.name + '</h2><p>';
          popup += properties.totalInstances + ' ' + this.translateService.instant('INSTANCES');
          popup += '<br>' + this.translateService.instant('MAP.POPUP.TYPE') + ': ' + properties.type;
          
          if (properties.description) {
            popup += '<br>' + this.translateService.instant('MAP.POPUP.REMARK') + ': ' + properties.description;
          }
          if (properties.lineNumber) {
            popup += '<br>' + this.translateService.instant('MAP.POPUP.LINENUMBER') + ': ' + properties.lineNumber;
          }
          if (properties.operatingCompany) {
            popup += '<br>' + this.translateService.instant('MAP.POPUP.OPERATINGCOMPANY') + ': ' + properties.operatingCompany;
          }
          if (properties.distance) {
            popup += '<br>' + this.translateService.instant('ROUTES.DISTANCE') + ': ' + properties.distance + ' km';
          }
          popup += '</p>';

          new maplibregl.Popup()
            .setLngLat(e.lngLat)
            .setHTML(popup)
            .addTo(this.maplibreMap!);
        }
      });

      // Change cursor on hover
      this.maplibreMap.on('mouseenter', 'routes-layer', () => {
        this.maplibreMap!.getCanvas().style.cursor = 'pointer';
      });
      this.maplibreMap.on('mouseleave', 'routes-layer', () => {
        this.maplibreMap!.getCanvas().style.cursor = '';
      });
    }

    // Fit bounds
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
