import { Component, OnInit, AfterViewInit, ChangeDetectorRef, NgZone, EventEmitter, OnDestroy, input, viewChild, signal, inject } from "@angular/core";
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

  readonly guid = input<string>(undefined);
  readonly mapContainer = viewChild<HTMLElement>("mapContainer");
  loading = signal<boolean | number>(false);
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
          this.loading.set(data.percentage);
          this.cd.detectChanges();
        }
      },
    });
  }

  ngOnDestroy() {
    this.signalRService.disconnect();
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
    this.loading.set(false);
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

    this.loading.set(true);
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
        value.selectedYears.every((y) => (years??[]).includes(y)) &&
        (years??[]).every((y) => value.selectedYears.includes(y))
      ) {
        this.active.set(key);
      }
    });
  }
}
