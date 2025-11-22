import { ChangeDetectorRef, Component, OnInit, OnDestroy, input, inject, ElementRef, viewChild } from "@angular/core";
import {
  LatLngBounds,
  LatLng,
  divIcon,
  circleMarker,
  LeafletEvent,
  MarkerClusterGroup,
  tileLayer
} from "leaflet";
import 'leaflet.markercluster';
import { ApiService } from "src/app/services/api.service";
import { TranslationService } from "src/app/services/translation.service";
import { MapProviderService } from "src/app/services/map-provider.service";
import { MapConfigService } from "src/app/services/map-config.service";
import * as maplibregl from 'maplibre-gl';
import { LeafletModule } from "@bluehalo/ngx-leaflet";
import { NgClass } from "@angular/common";
import { MatProgressSpinner } from "@angular/material/progress-spinner";
import { LeafletMarkerClusterModule } from "@bluehalo/ngx-leaflet-markercluster";

@Component({
    selector: "app-station-map",
    templateUrl: "./station-map.component.html",
    styleUrls: ["./station-map.component.scss"],
    imports: [
        LeafletModule,
        NgClass,
        MatProgressSpinner,
        LeafletMarkerClusterModule,
    ]
})
export class StationMapComponent implements OnInit, OnDestroy {
  private apiService = inject(ApiService);
  private translationService = inject(TranslationService);
  private cd = inject(ChangeDetectorRef);
  private mapProviderService = inject(MapProviderService);
  private mapConfigService = inject(MapConfigService);

  readonly maplibreContainer = viewChild<ElementRef<HTMLDivElement>>("maplibreContainer");
  mapProvider = this.mapProviderService.currentProvider;
  private maplibreMap: maplibregl.Map | null = null;
  private stationsData: any = null;
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
  names: { name: any; nameNL: any };
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
  get percentage() {
    if (!this.total || this.visited == undefined) {
      return "?";
    }
    return Math.round((this.visited / this.total) * 1000) / 10;
  }
  ngOnInit(): void {
    this.getData();
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

  async getData() {
    this.loading = true;

    const text = await this.apiService.getStationMap(this.guid()).toPromise();
    this.stationsData = text;
    const parent = this;
    this.total = text.total;
    this.visited = text.visited;
    this.names = {
      name: text.name,
      nameNL: text.nameNL,
    };

    if (this.mapProvider() === 'leaflet') {
      this.setupLeafletMap(text);
    } else {
      this.setupMapLibreMap(text);
    }
  }

  private setupLeafletMap(text: any) {
    const markers = window.L.markerClusterGroup({
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
    text.stations.forEach((station) => {
      const marker = circleMarker(
        new LatLng(station.lattitude, station.longitude, station.elevation),
        {
          radius: station.visited ? 8 : 4,
          fillColor: station.visited ? "#00FF00" : "#FF0000",
          color: "#000",
          weight: 1,
          opacity: 1,
          fillOpacity: station.visited ? 0.8 : 0.5,
        }
      );
      marker.feature = {
        properties: {
          id: station.id,
          visited: station.visited,
        },
        type: "Feature",
        geometry: null,
      };
      markers.addLayer(marker);
    });
    const parent = this;
    markers.addEventListener("click", async (f: LeafletEvent) => {
      if (!f.propagatedFrom.feature.properties.visited) {
        f.propagatedFrom.feature.properties.visited = true;
      } else {
        f.propagatedFrom.feature.properties.visited = false;
      }
      f.propagatedFrom.setStyle({
        fillColor: "#FF7F00",
        fillOpacity: 0.65,
        radius: 6,
      });
      (this.layers[0] as MarkerClusterGroup).refreshClusters();
      await parent.apiService
        .updateStation(
          f.propagatedFrom.feature.properties.id,
          f.propagatedFrom.feature.properties.visited
        )
        .toPromise();
      if (f.propagatedFrom.feature.properties.visited) {
        parent.visited++;
      } else {
        parent.visited--;
      }
      parent.cd.detectChanges();
      console.log(f.propagatedFrom);
      f.propagatedFrom.setStyle({
        fillColor: f.propagatedFrom.feature.properties.visited
          ? "#00FF00"
          : "#FF0000",
        fillOpacity: f.propagatedFrom.feature.properties.visited ? 0.8 : 0.5,
        radius: f.propagatedFrom.feature.properties.visited ? 8 : 4,
      });
      (this.layers[0] as MarkerClusterGroup).refreshClusters();
    });
    this.layers = [markers];
    this.bounds = markers.getBounds();
    this.loading = false;
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
      if (this.stationsData) {
        this.setupMapLibreMap(this.stationsData);
      }
    });
  }

  private setupMapLibreMap(text: any) {
    if (!this.maplibreMap || !this.maplibreMap.isStyleLoaded()) {
      return;
    }

    // Create GeoJSON features from stations
    const features = text.stations.map((station: any) => ({
      type: 'Feature',
      geometry: {
        type: 'Point',
        coordinates: [station.longitude, station.lattitude]
      },
      properties: {
        id: station.id,
        visited: station.visited
      }
    }));

    const geojson = {
      type: 'FeatureCollection',
      features: features
    };

    // Add source for stations
    this.maplibreMap.addSource('stations', {
      type: 'geojson',
      data: geojson as any,
      cluster: true,
      clusterMaxZoom: 9, // Max zoom to cluster points on (disable clustering at zoom 10+)
      clusterRadius: 40
    });

    // Add cluster circles
    this.maplibreMap.addLayer({
      id: 'clusters',
      type: 'circle',
      source: 'stations',
      filter: ['has', 'point_count'],
      paint: {
        'circle-color': [
          'case',
          ['all', ['==', ['get', 'visited'], true]], '#00FF00',
          ['all', ['==', ['get', 'visited'], false]], '#FF0000',
          '#FFA500'
        ],
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
          ['get', 'visited'], '#00FF00',
          '#FF0000'
        ],
        'circle-radius': [
          'case',
          ['get', 'visited'], 8,
          4
        ],
        'circle-stroke-width': 1,
        'circle-stroke-color': '#000',
        'circle-opacity': 1,
        'circle-stroke-opacity': 1
      }
    });

    // Handle click on unclustered point
    this.maplibreMap.on('click', 'unclustered-point', async (e) => {
      if (e.features && e.features.length > 0) {
        const feature = e.features[0];
        const stationId = feature.properties?.id;
        const currentVisited = feature.properties?.visited;
        
        if (stationId !== undefined) {
          const newVisited = !currentVisited;
          
          // Update via API
          await this.apiService.updateStation(stationId, newVisited).toPromise();
          
          // Update local state
          if (newVisited) {
            this.visited++;
          } else {
            this.visited--;
          }
          this.cd.detectChanges();
          
          // Update the feature in the source
          const source = this.maplibreMap!.getSource('stations') as maplibregl.GeoJSONSource;
          const data = source._data as any;
          const featureToUpdate = data.features.find((f: any) => f.properties.id === stationId);
          if (featureToUpdate) {
            featureToUpdate.properties.visited = newVisited;
            source.setData(data);
          }
        }
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
    if (features.length > 0) {
      const bounds = new maplibregl.LngLatBounds();
      features.forEach((feature: any) => {
        bounds.extend(feature.geometry.coordinates);
      });
      this.maplibreMap.fitBounds(bounds, { padding: 50 });
    }

    this.loading = false;
  }

  getName(object) {
    return this.translationService.getNameForItem(object);
  }
}
