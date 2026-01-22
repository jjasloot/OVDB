import { ChangeDetectorRef, Component, OnInit, input, inject, signal, computed } from "@angular/core";
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
export class StationMapComponent implements OnInit {
  private apiService = inject(ApiService);
  private translationService = inject(TranslationService);
  private cd = inject(ChangeDetectorRef);

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
  private _bounds = signal<LatLngBounds>(null);
  total = signal<number>(0);
  visited = signal<number>(0);
  names = signal<{ name: any; nameNL: any }>({ name: null, nameNL: null });
  layers = signal<any[]>([]);
  loading = signal(true);

  percentage = computed(() => {
    if (!this.total() || this.visited() == undefined) {
      return "?";
    }
    return Math.round((this.visited() / this.total()) * 1000) / 10;
  });

  get bounds(): LatLngBounds {
    return this._bounds();
  }
  set bounds(value: LatLngBounds) {
    if (!!value && value.isValid()) {
      this._bounds.set(value);
    } else {
      this._bounds.set(new LatLngBounds(
        new LatLng(50.656245, 2.92136),
        new LatLng(53.604563, 7.428211)
      ));
    }
  }
  leafletLayersControl = {
    baseLayers: this.baseLayers,
    // overlays: this.layers
  };
  ngOnInit(): void {
    this.getData();
  }

  async getData() {
    this.loading.set(true);

    const text = await this.apiService.getStationMap(this.guid()).toPromise();
    const parent = this;
    this.total.set(text.total);
    this.visited.set(text.visited);
    this.names.set({
      name: text.name,
      nameNL: text.nameNL,
    });
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
      (this.layers()[0] as MarkerClusterGroup).refreshClusters();
      await parent.apiService
        .updateStation(
          f.propagatedFrom.feature.properties.id,
          f.propagatedFrom.feature.properties.visited
        )
        .toPromise();
      if (f.propagatedFrom.feature.properties.visited) {
        parent.visited.update(v => v + 1);
      } else {
        parent.visited.update(v => v - 1);
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
      (this.layers()[0] as MarkerClusterGroup).refreshClusters();
    });
    this.layers.set([markers]);
    this.bounds = markers.getBounds();
    this.loading.set(false);
  }

  getName(object) {
    return this.translationService.getNameForItem(object);
  }
}
