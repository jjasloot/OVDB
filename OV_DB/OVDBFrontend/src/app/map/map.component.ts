import { Component, OnInit, ViewChild, Input, AfterViewInit, ChangeDetectorRef } from '@angular/core';
import * as moment from 'moment';
import { tileLayer } from 'leaflet';
import { ApiService } from '../services/api.service';
import * as L from 'leaflet';
import { FilterSettings } from '../models/filterSettings';
import { MatDialog } from '@angular/material/dialog';
import { Country } from '../models/country.model';
import { MapFilterComponent } from '../map-filter/map-filter.component';
import { TranslateService } from '@ngx-translate/core';
import { TranslationService } from '../services/translation.service';

@Component({
  selector: 'app-map',
  templateUrl: './map.component.html',
  styleUrls: ['./map.component.scss']
})
export class MapComponent implements OnInit, AfterViewInit {
  @Input() guid: string;
  @ViewChild('mapContainer') mapContainer: HTMLElement;
  loading = false;
  from: moment.Moment;
  to: moment.Moment;
  selectedCountries: number[] = [];
  selectedTypes: number[] = [];
  layers = [];
  countries: Country[];
  selectedYears: number[];
  error: boolean;
  active: string;
  selectedRoute;
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
  private _bounds: L.LatLngBounds;

  defaults = new Map<string, FilterSettings>([
    ['LastMonth',
      new FilterSettings(
        'LastMonth',
        moment().startOf('year'),
        moment().startOf('month'),
        []
      ),
    ],
    ['ThisYear',
      new FilterSettings(
        'ThisYear',
        null, null,
        [], [], [moment().year()]
      )
    ],
    ['LastYear',
      new FilterSettings(
        'LastYear',
        null, null,
        [], [], [moment().year() - 1]
      )
    ],
    ['All',
      new FilterSettings(
        'All',
        null, null, []
      )
    ]

  ]
  );

  get mapHeight() {
    if (this.mapContainer) {
      return this.mapContainer.offsetHeight
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
    private apiService: ApiService, private dialog: MatDialog, private cd: ChangeDetectorRef) { }
  ngAfterViewInit(): void {
    this.cd.detectChanges();
  }



  ngOnInit() {
    this.setOption(this.defaults.get('ThisYear'));
    this.translationService.languageChanged.subscribe(() => this.getRoutes())
  }



  private async getRoutes() {
    try {
      this.loading = true;
      let filter = '';
      if (!!this.to && !!this.from) {
        filter += 'FirstDateTime ge ' + this.from.format('YYYY-MM-DD') + ' and ';
        filter += 'FirstDateTime lt ' + this.to.format('YYYY-MM-DD') + ' and ';
      }
      if (this.selectedCountries && this.selectedCountries.length > 0) {
        filter += '(';
        this.selectedCountries.forEach(option => {
          filter += 'RouteCountries/any(routeCountry: routeCountry/CountryId eq ' + option + ') or ';
        });
        if (filter.endsWith(' or ')) {
          filter = filter.slice(0, filter.length - 4);
        }
        filter += ') and ';
      }
      if (this.selectedTypes && this.selectedTypes.length > 0) {
        filter += '(';
        this.selectedTypes.forEach(option => {
          filter += 'RouteTypeId eq ' + option + ' or ';
        });
        if (filter.endsWith(' or ')) {
          filter = filter.slice(0, filter.length - 4);
        }
        filter += ') and ';
      }

      if (this.selectedYears && this.selectedYears.length > 0) {
        filter += '(';
        this.selectedYears.forEach(option => {
          if (!option) {
            filter += 'FirstDateTime eq null or ';
          } else {
            const start = moment().year(option).startOf('year');
            const end = moment().year(option + 1).startOf('year');

            filter += '(FirstDateTime ge '
              + start.format('YYYY-MM-DD')
              + ' and FirstDateTime lt '
              + end.format('YYYY-MM-DD') + ') or ';
          }
        });
        if (filter.endsWith(' or ')) {
          filter = filter.slice(0, filter.length - 4);
        }
        filter += ') and ';
      }

      if (filter.endsWith(' and ')) {
        filter = filter.slice(0, filter.length - 5);
      }
      const text = await this.apiService.getRoutes(filter, this.guid, this.translationService.language).toPromise();
      const parent = this;
      const track = L.geoJSON(text as any, {
        style: feature => {
          return {
            color: feature.properties.stroke,
            weight: 3
          };
        },
        onEachFeature(feature, layer) {
          if (!!feature.properties.name) {
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
            if (!!feature.properties.distance) {
              popup += '<br>' + parent.translateService.instant('ROUTES.DISTANCE') + ': ' + feature.properties.distance + ' km';
            }
            popup += '</p>';
            layer.bindPopup(popup);
          }
          if (!!feature.properties.o) {
            layer.on('click', f => {
              if (!!parent.selectedRoute) {
                parent.selectedRoute.setStyle({ weight: 3 });
              }
              parent.selectedRoute = f.target;
              f.target.setStyle({ weight: 8, })
              f.target.bringToFront();
              if (!!feature.properties.name) {
                f.target.getPopup().on('remove', () => {
                  f.target.setStyle({
                    weight: 3,
                  });
                });
              }
            });
          }
        }
      });
      this.layers = [track];
      this.bounds = track.getBounds();
      this.loading = false;
    }
    catch{
      this.error = true;
    }
  }

  setOption(option: FilterSettings) {
    this.from = option.from;
    this.to = option.to;
    this.selectedCountries = [...option.selectedCountries];
    this.selectedTypes = [...option.selectedTypes];
    this.selectedYears = [...option.selectedYears];
    this.active = option.name;
    this.getRoutes();
  }


  openDialog() {
    const settings = new FilterSettings(
      '',
      this.from,
      this.to,
      this.selectedCountries,
      this.selectedTypes,
      this.selectedYears);
    const dialogRef = this.dialog.open(MapFilterComponent, {
      width: '50%',
      data: { settings, guid: this.guid }
    });
    dialogRef.afterClosed().subscribe(result => {
      if (!!result) {
        result.name = 'filter'
        this.setOption(result);
      }
    });
  }
}
