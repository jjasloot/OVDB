import { ChangeDetectionStrategy, Component, OnInit, computed, inject, signal } from '@angular/core';
import { DecimalPipe } from '@angular/common';
import { MatButtonToggleModule } from '@angular/material/button-toggle';
import { MatCard, MatCardContent, MatCardTitle } from '@angular/material/card';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatProgressSpinner } from '@angular/material/progress-spinner';
import { MatSelectModule } from '@angular/material/select';
import { TranslateModule, TranslateService } from '@ngx-translate/core';
import { LeafletModule } from '@bluehalo/ngx-leaflet';
import { LatLng, LatLngBounds, Layer, Polyline, geoJSON, latLng, polyline } from 'leaflet';
import type { Feature, FeatureCollection, GeoJsonObject, LineString, Position } from 'geojson';
import { forkJoin } from 'rxjs';
import { MapTileLayersService } from 'src/app/services/map-tile-layers.service';
import { NetworkCompletionService } from 'src/app/services/network-completion.service';
import { TranslationService } from 'src/app/services/translation.service';
import {
  CoveredEdge,
  MissingRun,
  NetworkCompletion,
  NetworkGeometry,
  NetworkGeometryEdge,
  NetworkMode,
  RegionNetworks,
} from 'src/app/models/network-completion.model';

/**
 * The rail network of a region with every ridden metre drawn exactly (docs/region-network-
 * completion.md §7). The geometry payload is user-independent and cached by the browser; the
 * completion payload carries per-edge covered runs as fractions of the drawn polyline's length,
 * which this component cuts client-side — so green ends where the ride ended.
 */
@Component({
  selector: 'app-network-completion',
  templateUrl: './network-completion.component.html',
  styleUrl: './network-completion.component.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [
    DecimalPipe,
    LeafletModule,
    MatButtonToggleModule,
    MatCard,
    MatCardContent,
    MatCardTitle,
    MatFormFieldModule,
    MatProgressSpinner,
    MatSelectModule,
    TranslateModule,
  ],
})
export class NetworkCompletionComponent implements OnInit {
  private service = inject(NetworkCompletionService);
  private translationService = inject(TranslationService);
  private translateService = inject(TranslateService);
  private mapTileLayersService = inject(MapTileLayersService);

  readonly NetworkMode = NetworkMode;

  loading = signal(true);
  loadingNetwork = signal(false);
  regions = signal<RegionNetworks[]>([]);
  selectedRegionId = signal<number | null>(null);
  selectedMode = signal<NetworkMode>(NetworkMode.Rail);

  geometry = signal<NetworkGeometry | null>(null);
  completion = signal<NetworkCompletion | null>(null);
  missing = signal<MissingRun[]>([]);
  layers = signal<Layer[]>([]);
  /** Falls back to the Netherlands until a network is loaded; the fit-bounds input cannot take null. */
  bounds = signal<LatLngBounds>(new LatLngBounds(new LatLng(50.656245, 2.92136), new LatLng(53.604563, 7.428211)));

  selectedRegion = computed(() => this.regions().find((r) => r.regionId === this.selectedRegionId()) ?? null);
  /** Modes the selected region actually has a network for. */
  availableModes = computed(() => this.selectedRegion()?.networks.map((n) => n.mode) ?? []);
  selectedNetwork = computed(() => this.selectedRegion()?.networks.find((n) => n.mode === this.selectedMode()) ?? null);

  baseLayers = this.mapTileLayersService.createBaseLayers();
  options = {
    layers: [this.mapTileLayersService.defaultLayer(this.baseLayers)],
    zoom: 7,
    center: latLng(52.2, 5.3),
  };
  leafletLayersControl = { baseLayers: this.baseLayers, overlays: {} };

  private edgeLayers = new Map<number, Layer[]>();

  ngOnInit(): void {
    this.service.regions().subscribe({
      next: (regions) => {
        this.regions.set(regions);
        this.loading.set(false);
        if (regions.length > 0) {
          this.selectRegion(regions[0].regionId);
        }
      },
      error: () => this.loading.set(false),
    });
  }

  regionName(region: RegionNetworks): string {
    return this.translationService.getNameForItem(region);
  }

  selectRegion(regionId: number): void {
    this.selectedRegionId.set(regionId);
    const modes = this.availableModes();
    if (!modes.includes(this.selectedMode()) && modes.length > 0) {
      this.selectedMode.set(modes[0]);
    }
    this.loadNetwork();
  }

  selectMode(mode: NetworkMode): void {
    this.selectedMode.set(mode);
    this.loadNetwork();
  }

  /** Zoom to the edges of one missing run. */
  focusMissing(run: MissingRun): void {
    const bounds = new LatLngBounds([]);
    for (const id of run.edgeIds) {
      for (const layer of this.edgeLayers.get(id) ?? []) {
        if (layer instanceof Polyline) {
          const layerBounds = layer.getBounds();
          if (layerBounds.isValid()) bounds.extend(layerBounds);
        }
      }
    }
    if (bounds.isValid()) this.bounds.set(bounds.pad(0.2));
  }

  runLabel(run: MissingRun): string {
    const name = run.name || this.translateService.instant('STATS.NETWORK.UNNAMED');
    if (run.fromStation || run.toStation) {
      return `${name} · ${run.fromStation ?? '…'} – ${run.toStation ?? '…'}`;
    }
    return name;
  }

  private loadNetwork(): void {
    const network = this.selectedNetwork();
    if (!network) {
      this.layers.set([]);
      this.geometry.set(null);
      this.completion.set(null);
      this.missing.set([]);
      return;
    }
    this.loadingNetwork.set(true);
    forkJoin({
      geometry: this.service.geometry(network.id),
      completion: this.service.completion(network.id),
      missing: this.service.missing(network.id),
    }).subscribe({
      next: ({ geometry, completion, missing }) => {
        this.geometry.set(geometry);
        this.completion.set(completion);
        this.missing.set(missing);
        this.buildLayers(geometry, completion);
        this.loadingNetwork.set(false);
      },
      error: () => this.loadingNetwork.set(false),
    });
  }

  private buildLayers(geometry: NetworkGeometry, completion: NetworkCompletion): void {
    this.edgeLayers.clear();
    const covered = new Map<number, CoveredEdge>(completion.edges.map((e) => [e.id, e]));

    // Base: every edge, thin, in its "not ridden" colour.
    const collection: FeatureCollection<LineString, NetworkGeometryEdge> = {
      type: 'FeatureCollection',
      features: geometry.edges.map(
        (edge): Feature<LineString, NetworkGeometryEdge> => ({
          type: 'Feature',
          properties: edge,
          geometry: edge.geometry as LineString,
        })
      ),
    };
    const base = geoJSON(collection, {
      style: (feature) => this.baseStyle(feature!.properties as NetworkGeometryEdge),
      onEachFeature: (feature, layer) => {
        const edge = feature.properties as NetworkGeometryEdge;
        layer.bindTooltip(this.tooltip(edge), { sticky: true });
        this.edgeLayers.set(edge.id, [layer]);
      },
    });

    // Coverage on top: the covered runs of each edge, cut from the drawn polyline by length fraction.
    const overlays: Layer[] = [];
    for (const edge of geometry.edges) {
      const runs = covered.get(edge.id);
      if (!runs) continue;
      const latlngs = this.toLatLngs(edge.geometry);
      for (const [start, end] of runs.intervals) {
        const piece = this.cutByFraction(latlngs, start, end);
        if (piece.length < 2) continue;
        const line = polyline(piece, this.coveredStyle(edge)).bindTooltip(this.tooltip(edge), { sticky: true });
        overlays.push(line);
        this.edgeLayers.get(edge.id)?.push(line);
      }
    }

    this.layers.set([base, ...overlays]);
    const bounds = base.getBounds();
    if (bounds.isValid()) this.bounds.set(bounds);
  }

  private baseStyle(edge: NetworkGeometryEdge) {
    return edge.isPassenger
      ? { color: '#c62828', weight: 1.8, opacity: 0.9 }
      : { color: '#e8a3a3', weight: 1.2, opacity: 0.7, dashArray: '4 4' };
  }

  private coveredStyle(edge: NetworkGeometryEdge) {
    return edge.isPassenger
      ? { color: '#2e7d32', weight: 3.8, opacity: 0.95 }
      : { color: '#00838f', weight: 3, opacity: 0.95, dashArray: '6 4' };
  }

  private tooltip(edge: NetworkGeometryEdge): string {
    const name = edge.name || this.translateService.instant('STATS.NETWORK.UNNAMED');
    const span = edge.fromStation || edge.toStation ? `<br>${edge.fromStation ?? '…'} – ${edge.toStation ?? '…'}` : '';
    const kind = edge.isPassenger ? '' : `<br><i>${this.translateService.instant('STATS.NETWORK.NON_PASSENGER')}</i>`;
    return `<b>${name}</b>${span}<br>${edge.lengthKm.toFixed(1)} km${kind}`;
  }

  private toLatLngs(geometry: GeoJsonObject): LatLng[] {
    const coords: Position[] = geometry.type === 'LineString' ? (geometry as LineString).coordinates : [];
    return coords.map(([lng, lat]) => latLng(lat, lng));
  }

  /** Cut a polyline by fractions of its own length — exactly what the server's intervals express. */
  private cutByFraction(latlngs: LatLng[], start: number, end: number): LatLng[] {
    const cumulative = [0];
    for (let i = 1; i < latlngs.length; i++) {
      cumulative.push(cumulative[i - 1] + latlngs[i - 1].distanceTo(latlngs[i]));
    }
    const total = cumulative[cumulative.length - 1];
    if (total <= 0) return [];
    const startM = start * total;
    const endM = end * total;
    const pointAt = (m: number): LatLng => {
      let k = 1;
      while (k < cumulative.length - 1 && cumulative[k] < m) k++;
      const segment = cumulative[k] - cumulative[k - 1];
      const t = segment <= 0 ? 0 : (m - cumulative[k - 1]) / segment;
      return latLng(
        latlngs[k - 1].lat + (latlngs[k].lat - latlngs[k - 1].lat) * t,
        latlngs[k - 1].lng + (latlngs[k].lng - latlngs[k - 1].lng) * t
      );
    };
    const piece = [pointAt(startM)];
    for (let i = 0; i < latlngs.length; i++) {
      if (cumulative[i] > startM && cumulative[i] < endM) piece.push(latlngs[i]);
    }
    piece.push(pointAt(endM));
    return piece;
  }
}
