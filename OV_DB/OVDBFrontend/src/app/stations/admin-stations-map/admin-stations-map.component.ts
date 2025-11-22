import {
  ChangeDetectorRef,
  Component,
  OnInit,
  OnDestroy,
  inject,
  input,
  signal,
  ElementRef,
  viewChild
} from "@angular/core";
import { MatCheckboxChange, MatCheckbox } from "@angular/material/checkbox";
import { LatLngBounds, LatLng, markerClusterGroup, divIcon, circleMarker } from "leaflet";
import { tileLayer } from "leaflet";
import { Region } from "src/app/models/region.model";
import { StationAdminProperties } from "src/app/models/stationAdminProperties.model";
import { ApiService } from "src/app/services/api.service";
import { RegionsService } from "src/app/services/regions.service";
import { TranslationService } from "src/app/services/translation.service";
import { MapProviderService } from "src/app/services/map-provider.service";
import { MapConfigService } from "src/app/services/map-config.service";
import * as maplibregl from 'maplibre-gl';
import { LeafletModule } from "@bluehalo/ngx-leaflet";
import { KeyValuePipe, NgClass } from "@angular/common";
import { MatProgressSpinner } from "@angular/material/progress-spinner";
import { MatExpansionPanel, MatExpansionPanelHeader } from "@angular/material/expansion";
import { MatButton, MatIconButton } from "@angular/material/button";
import { TooltipComponent, MatTooltip } from "@angular/material/tooltip";
import { MatFormField, MatLabel } from "@angular/material/form-field";
import { MatInput } from "@angular/material/input";
import { FormsModule } from "@angular/forms";
import { MatIcon } from "@angular/material/icon";
import { MatSelect } from "@angular/material/select";
import { MatOption } from "@angular/material/core";
import { LeafletMarkerClusterModule } from "@bluehalo/ngx-leaflet-markercluster";
import { SignalRService } from "src/app/services/signal-r.service";
import { MatChip } from "@angular/material/chips";
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
@Component({
  selector: "app-admin-stations-map",
  templateUrl: "./admin-stations-map.component.html",
  styleUrls: ["./admin-stations-map.component.scss"],
  imports: [
    LeafletModule,
    NgClass,
    MatProgressSpinner,
    MatExpansionPanel,
    MatExpansionPanelHeader,
    MatButton,
    MatCheckbox,
    TooltipComponent,
    MatTooltip,
    MatFormField,
    MatLabel,
    MatInput,
    FormsModule,
    MatIconButton,
    MatIcon,
    MatSelect,
    MatOption,
    LeafletMarkerClusterModule,
    MatChip,
    KeyValuePipe,
  ]
})
export class AdminStationsMapComponent implements OnInit, OnDestroy {
  private apiService = inject(ApiService);
  private cd = inject(ChangeDetectorRef);
  private mapProviderService = inject(MapProviderService);
  private mapConfigService = inject(MapConfigService);

  regionsService = inject(RegionsService);
  translationService = inject(TranslationService);
  signalRService = inject(SignalRService);
  
  readonly maplibreContainer = viewChild<ElementRef<HTMLDivElement>>("maplibreContainer");
  mapProvider = this.mapProviderService.currentProvider;
  private maplibreMap: maplibregl.Map | null = null;
  private stationsData: any[] = [];
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
  readonly guid = input<string>(undefined);
  options = {
    layers: [this.baseLayers["OpenStreetMap Mat"]],
    zoom: 5,
  };
  private _bounds: LatLngBounds;
  total: number;
  visited: number;
  regions: Region[] = [];
  selectedRegions: number[] = [];
  selectedStation = signal<StationAdminProperties | null>(null);
  allRegions = signal<Region[]>([]);
  selectedRegionId?: number;
  selectedNewStationId?: string;
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
  leafletLayersControl = {
    baseLayers: this.baseLayers,
    // overlays: this.layers
  };
  layers = [];

  loading = true;

  progressUpdates: Record<number, number> = {};


  constructor() {
    this.signalRService.stationUpdates$.pipe(takeUntilDestroyed()).subscribe(update => {
      this.progressUpdates[update.regionId] = update.percentage;
      if (update.percentage >= 100) {
        this.getData();
      }
    });
  }
  get percentage() {
    if (this.total === 0) {
      return "?";
    }
    return Math.round((this.visited / this.total) * 1000) / 10;
  }
  ngOnInit(): void {
    this.getData(true);
    this.getRegions();
    if (this.mapProvider() === 'maplibre') {
      setTimeout(() => this.initMapLibre(), 100);
    }
  }

  ngOnDestroy(): void {
    if (this.maplibreMap) {
      this.maplibreMap.remove();
      this.maplibreMap = null;
    }
  }

  getRegions() {
    this.regionsService.getRegions().subscribe({
      next: (regions) => {
        this.regions = regions;
        this.setAllRegions();
      },
      error: (error) => {
        console.error(error);
      },
    });
  }

  getNameById(regionId: number) {
    return this.name(this.regions.find(r => r.id == regionId));
  }

  async getData(updateBounds = false) {
    this.loading = true;

    const text = await this.apiService
      .getStationsAdminMap(this.selectedRegions)
      .toPromise();
    this.stationsData = text;

    if (this.mapProvider() === 'leaflet') {
      this.setupLeafletMap(text, updateBounds);
    } else {
      this.setupMapLibreMap(text, updateBounds);
    }
  }

  private setupLeafletMap(text: any[], updateBounds: boolean) {
    const parent = this;
    const markers = markerClusterGroup({
      iconCreateFunction: (cluster) => {
        return divIcon({
          html: "<b>" + cluster.getChildCount() + "</b>",
          className: cluster
            .getAllChildMarkers()
            .every((r) => r.feature.properties.visited)
            ? "green"
            : cluster
              .getAllChildMarkers()
              .every((r) => !r.feature.properties.visited)
              ? "red"
              : "orange",
        });
      },
      disableClusteringAtZoom: 10,
      maxClusterRadius: 40,
    });
    text.forEach((station) => {
      const marker = circleMarker(
        new LatLng(station.lattitude, station.longitude),
        {
          radius: 6,
          fillColor: station.hidden
            ? "#FF0000"
            : station.special
              ? "#0000FF"
              : "#00FF00",
          color: "#000",
          weight: 1,
          opacity: 1,
          fillOpacity: 0.65,
        }
      );
      marker.feature = {
        properties: station,
        type: "Feature",
        geometry: null,
      };
      markers.addLayer(marker);
    });
    markers.addEventListener("click", async (f) => {
      this.selectedStation.set(f.propagatedFrom.feature.properties as StationAdminProperties);
      this.cd.detectChanges();
    });

    this.layers = [markers];
    if (updateBounds) {
      this.bounds = markers.getBounds();
    }
    this.loading = false;
    this.cd.detectChanges();
  }

  private initMapLibre() {
    const container = this.maplibreContainer();
    if (!container || this.maplibreMap) {
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
      if (this.stationsData && this.stationsData.length > 0) {
        this.setupMapLibreMap(this.stationsData, true);
      }
    });
  }

  private setupMapLibreMap(text: any[], updateBounds: boolean) {
    if (!this.maplibreMap || !this.maplibreMap.isStyleLoaded()) {
      return;
    }

    // Create GeoJSON features
    const features = text.map((station: any) => ({
      type: 'Feature',
      geometry: {
        type: 'Point',
        coordinates: [station.longitude, station.lattitude]
      },
      properties: {
        id: station.id,
        hidden: station.hidden,
        special: station.special,
        ...station
      }
    }));

    const geojson = {
      type: 'FeatureCollection',
      features: features
    };

    // Remove existing layers and source if they exist
    if (this.maplibreMap.getLayer('clusters')) {
      this.maplibreMap.removeLayer('clusters');
    }
    if (this.maplibreMap.getLayer('cluster-count')) {
      this.maplibreMap.removeLayer('cluster-count');
    }
    if (this.maplibreMap.getLayer('unclustered-point')) {
      this.maplibreMap.removeLayer('unclustered-point');
    }
    if (this.maplibreMap.getSource('stations')) {
      this.maplibreMap.removeSource('stations');
    }

    // Add source
    this.maplibreMap.addSource('stations', {
      type: 'geojson',
      data: geojson as any,
      cluster: true,
      clusterMaxZoom: 9,
      clusterRadius: 40
    });

    // Add cluster circles
    this.maplibreMap.addLayer({
      id: 'clusters',
      type: 'circle',
      source: 'stations',
      filter: ['has', 'point_count'],
      paint: {
        'circle-color': '#FFA500',
        'circle-radius': [
          'step',
          ['get', 'point_count'],
          15,
          10, 20,
          30, 25
        ]
      }
    });

    // Add cluster count
    this.maplibreMap.addLayer({
      id: 'cluster-count',
      type: 'symbol',
      source: 'stations',
      filter: ['has', 'point_count'],
      layout: {
        'text-field': '{point_count_abbreviated}',
        'text-font': ['Open Sans Bold'],
        'text-size': 12
      }
    });

    // Add unclustered points
    this.maplibreMap.addLayer({
      id: 'unclustered-point',
      type: 'circle',
      source: 'stations',
      filter: ['!', ['has', 'point_count']],
      paint: {
        'circle-color': [
          'case',
          ['get', 'hidden'], '#FF0000',
          ['get', 'special'], '#0000FF',
          '#00FF00'
        ],
        'circle-radius': 6,
        'circle-stroke-width': 1,
        'circle-stroke-color': '#000',
        'circle-opacity': 0.65,
        'circle-stroke-opacity': 1
      }
    });

    // Handle click on unclustered point
    this.maplibreMap.on('click', 'unclustered-point', (e) => {
      if (e.features && e.features.length > 0) {
        const feature = e.features[0];
        this.selectedStation.set(feature.properties as StationAdminProperties);
        this.cd.detectChanges();
      }
    });

    // Handle click on cluster
    this.maplibreMap.on('click', 'clusters', (e) => {
      const features = this.maplibreMap!.queryRenderedFeatures(e.point, {
        layers: ['clusters']
      });
      const clusterId = features[0].properties?.cluster_id;
      const source = this.maplibreMap!.getSource('stations') as maplibregl.GeoJSONSource;
      
      (source as any).getClusterExpansionZoom(clusterId, (err: any, zoom: any) => {
        if (err) return;
        this.maplibreMap!.easeTo({
          center: (features[0].geometry as any).coordinates,
          zoom: zoom
        });
      });
    });

    // Change cursor on hover
    this.maplibreMap.on('mouseenter', 'clusters', () => {
      this.maplibreMap!.getCanvas().style.cursor = 'pointer';
    });
    this.maplibreMap.on('mouseleave', 'clusters', () => {
      this.maplibreMap!.getCanvas().style.cursor = '';
    });
    this.maplibreMap.on('mouseenter', 'unclustered-point', () => {
      this.maplibreMap!.getCanvas().style.cursor = 'pointer';
    });
    this.maplibreMap.on('mouseleave', 'unclustered-point', () => {
      this.maplibreMap!.getCanvas().style.cursor = '';
    });

    // Fit bounds
    if (updateBounds && features.length > 0) {
      const bounds = new maplibregl.LngLatBounds();
      features.forEach((feature: any) => {
        bounds.extend(feature.geometry.coordinates);
      });
      this.maplibreMap.fitBounds(bounds, { padding: 50 });
    }

    this.loading = false;
    this.cd.detectChanges();
  }

  isRegionChecked(id: number) {
    return this.selectedRegions.includes(id);
  }

  setRegion(id: number, event: MatCheckboxChange, subRegions?: Region[]) {
    if (event.checked && !this.selectedRegions.includes(id)) {
      this.selectedRegions.push(id);
      this.selectedRegions = this.selectedRegions.filter(
        (i) => !subRegions.map((r) => r.id).includes(i)
      );
    }
    if (!event.checked && this.selectedRegions.includes(id)) {
      this.selectedRegions = this.selectedRegions.filter((i) => i !== id);
      this.selectedRegions = this.selectedRegions.filter(
        (i) => !subRegions.map((r) => r.id).includes(i)
      );
    }
  }

  anyChecked(regions: Region[]) {
    return regions.some((r) => this.selectedRegions.includes(r.id));
  }

  name(item: Region) {
    return this.translationService.getNameForItem(item);
  }

  deleteStation() {
    this.apiService
      .deleteStationAdmin(this.selectedStation().id)
      .subscribe(() => {
        this.getData();
        this.selectedStation.set(null);
      });
  }

  setAllRegions() {
    const list = this.regions;
    this.regions.forEach((region) => {
      list.push(...region.subRegions);
    });
    this.allRegions.set(list);
  }

  updateRegion() {
    if (!this.selectedRegionId) return;
    this.apiService
      .updateStationsInRegion(this.selectedRegionId)
      .subscribe(() => {
        this.getData();
      });
  }
  loadNewStation() {
    if (!this.selectedNewStationId) return;
    this.apiService.importStation(this.selectedNewStationId).subscribe(() => {
      this.getData();
      this.selectedNewStationId = undefined;
    });
  }

  updateStation(hidden = false, special = false) {
    if (!this.selectedStation()) {
      return;
    }
    this.apiService.updateStationAdmin(this.selectedStation().id, special, hidden).subscribe(() => {
      this.getData();
      const station = this.selectedStation();
      station.hidden = hidden;
      station.special = special;
      this.selectedStation.set(station);
    });
  }
}
