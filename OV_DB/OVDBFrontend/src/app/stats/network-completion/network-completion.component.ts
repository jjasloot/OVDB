import { ChangeDetectionStrategy, Component, DestroyRef, OnInit, computed, inject, signal } from '@angular/core';
import { DatePipe, DecimalPipe } from '@angular/common';
import { MatButton, MatIconButton } from '@angular/material/button';
import { MatButtonToggleModule } from '@angular/material/button-toggle';
import { MatCard, MatCardContent, MatCardTitle } from '@angular/material/card';
import { MatIcon } from '@angular/material/icon';
import { MatSnackBar } from '@angular/material/snack-bar';
import { AuthenticationService } from 'src/app/services/authentication.service';
import { SignalRService } from 'src/app/services/signal-r.service';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatProgressSpinner } from '@angular/material/progress-spinner';
import { MatSelectModule } from '@angular/material/select';
import { TranslateModule, TranslateService } from '@ngx-translate/core';
import { LeafletModule } from '@bluehalo/ngx-leaflet';
import { LatLng, LatLngBounds, Layer, Map as LeafletMap, Polyline, geoJSON, latLng, polyline } from 'leaflet';
import type { Feature, FeatureCollection, GeoJsonObject, LineString, Position } from 'geojson';
import { Observable, Subject, forkJoin, of } from 'rxjs';
import { catchError, map, switchMap } from 'rxjs/operators';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
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
  NetworkOverrideGroup,
  NetworkOverrideKind,
  NetworkWayOverride,
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
    DatePipe,
    DecimalPipe,
    LeafletModule,
    MatButton,
    MatButtonToggleModule,
    MatCard,
    MatCardContent,
    MatCardTitle,
    MatFormFieldModule,
    MatIcon,
    MatIconButton,
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
  private authService = inject(AuthenticationService);
  private signalRService = inject(SignalRService);
  private snackBar = inject(MatSnackBar);
  private destroyRef = inject(DestroyRef);

  readonly NetworkMode = NetworkMode;
  readonly NetworkOverrideKind = NetworkOverrideKind;

  /** Curation is admin-only; the endpoints enforce the same rule. */
  readonly isAdmin = this.authService.admin;
  /** The edge last clicked on the map, the subject of the curation actions. */
  selectedEdge = signal<NetworkGeometryEdge | null>(null);
  overrides = signal<NetworkWayOverride[]>([]);
  /** One entry per curation action (same kind and timestamp): a curated stretch is many OSM ways. */
  overrideGroups = computed<NetworkOverrideGroup[]>(() => {
    const groups = new Map<string, NetworkOverrideGroup>();
    for (const o of this.overrides()) {
      const key = `${o.kind}|${o.createdAt}`;
      const group = groups.get(key) ?? { kind: o.kind, createdAt: o.createdAt, osmWayIds: [] };
      group.osmWayIds.push(o.osmWayId);
      groups.set(key, group);
    }
    return [...groups.values()];
  });
  overriding = signal(false);

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
  /** The network the map was last fitted to: reloads of the same network keep the user's view. */
  private fittedNetworkId: number | null = null;

  /** The selected network id; switchMap makes a fast second selection win over a slow first response. */
  private selection$ = new Subject<number | null>();
  /** The region whose overrides to show; switchMap drops a slow answer for a region no longer selected. */
  private overrides$ = new Subject<number | null>();

  constructor() {
    this.selection$
      .pipe(
        switchMap((networkId) => {
          if (networkId == null) {
            return of({ networkId, result: null, failed: false });
          }
          this.loadingNetwork.set(true);
          return this.fetchNetwork(networkId, 1).pipe(
            map((result) => ({ networkId, result, failed: false })),
            catchError(() => of({ networkId, result: null, failed: true }))
          );
        }),
        takeUntilDestroyed()
      )
      .subscribe(({ networkId, result, failed }) => {
        this.loadingNetwork.set(false);
        if (failed) {
          this.snackBar.open(this.translateService.instant('STATS.NETWORK.LOAD_FAILED'), undefined, { duration: 5000 });
        }
        if (!result) {
          this.layers.set([]);
          this.geometry.set(null);
          this.completion.set(null);
          this.missing.set([]);
          this.fittedNetworkId = null;
          return;
        }
        this.geometry.set(result.geometry);
        this.completion.set(result.completion);
        this.missing.set(result.missing);
        this.buildLayers(result.geometry, result.completion, networkId);
        // The selected edge belongs to the old version when the network was republished.
        const selected = this.selectedEdge();
        if (selected && !result.geometry.edges.some((e) => e.id === selected.id)) {
          this.selectedEdge.set(null);
        }
      });

    this.overrides$
      .pipe(
        switchMap((regionId) =>
          !this.isAdmin || regionId == null
            ? of([] as NetworkWayOverride[])
            : this.service.overrides(regionId).pipe(catchError(() => of([] as NetworkWayOverride[])))
        ),
        takeUntilDestroyed()
      )
      .subscribe((overrides) => this.overrides.set(overrides));

    // The hub connection is shared app-wide; make sure it exists when this page is opened directly.
    this.signalRService.connect();

    // A reassembly (after an override, or an admin refresh) reports 100 % once its new version
    // is committed: reload the region list, keep the selection valid, and redraw.
    this.signalRService.networkUpdates$.pipe(takeUntilDestroyed()).subscribe((update) => {
      if (update.percentage >= 100 && update.regionId === this.selectedRegionId()) {
        this.reloadRegions();
      }
    });
    // An admin rematch rewrites the coverage of one network without changing its geometry.
    this.signalRService.networkMatchUpdates$.pipe(takeUntilDestroyed()).subscribe((update) => {
      if (update.percentage >= 100 && update.networkId === this.selectedNetwork()?.id) {
        this.loadNetwork();
      }
    });
  }

  /**
   * Geometry and completion of one network. A republish can land between the two requests; the
   * completion then describes edges the geometry doesn't have, so fetch again (bounded).
   */
  private fetchNetwork(
    networkId: number,
    attempt: number
  ): Observable<{ geometry: NetworkGeometry; completion: NetworkCompletion; missing: MissingRun[] }> {
    return forkJoin({
      geometry: this.service.geometry(networkId),
      completion: this.service.completion(networkId),
      missing: this.service.missing(networkId),
    }).pipe(
      switchMap((result) =>
        result.geometry.version !== result.completion.version && attempt < 3
          ? this.fetchNetwork(networkId, attempt + 1)
          : of(result)
      )
    );
  }

  private reloadRegions(): void {
    this.service.regions().subscribe({
      next: (regions) => {
        this.regions.set(regions);
        const regionId = this.selectedRegionId();
        if (regionId != null && regions.some((r) => r.regionId === regionId)) {
          this.ensureValidMode();
          this.loadNetwork();
          this.loadOverrides();
        } else if (regions.length > 0) {
          this.selectRegion(regions[0].regionId);
        } else {
          this.selectedRegionId.set(null);
          this.loadNetwork();
          this.loadOverrides();
        }
      },
      error: () => this.snackBar.open(this.translateService.instant('STATS.NETWORK.LOAD_FAILED'), undefined, { duration: 5000 }),
    });
  }

  /** Keep the mode toggle on a mode the selected region has. */
  private ensureValidMode(): void {
    const modes = this.availableModes();
    if (!modes.includes(this.selectedMode()) && modes.length > 0) {
      this.selectedMode.set(modes[0]);
    }
  }

  // ---- curation (admin) ----------------------------------------------------------------------

  overrideKindLabel(kind: NetworkOverrideKind): string {
    switch (kind) {
      case NetworkOverrideKind.ForceRail:
        return 'STATS.NETWORK.KIND_RAIL';
      case NetworkOverrideKind.ForceUrbanRail:
        return 'STATS.NETWORK.KIND_URBAN';
      default:
        return 'STATS.NETWORK.KIND_EXCLUDED';
    }
  }

  /** Apply an override to every OSM way of the selected edge; the region reassembles in the background. */
  applyOverride(kind: NetworkOverrideKind): void {
    const edge = this.selectedEdge();
    const regionId = this.selectedRegionId();
    if (!edge || regionId == null) return;
    this.overriding.set(true);
    this.service
      .edgeWays(edge.id)
      .pipe(switchMap((ways) => this.service.addOverrides(regionId, ways, kind)))
      .subscribe({
        next: (overrides) => {
          if (this.selectedRegionId() === regionId) this.overrides.set(overrides);
          this.overriding.set(false);
          this.selectedEdge.set(null);
          this.snackBar.open(this.translateService.instant('STATS.NETWORK.OVERRIDE_QUEUED'), undefined, { duration: 4000 });
        },
        error: () => {
          this.overriding.set(false);
          this.snackBar.open(this.translateService.instant('STATS.NETWORK.OVERRIDE_FAILED'), undefined, { duration: 5000 });
        },
      });
  }

  removeOverrideGroup(group: NetworkOverrideGroup): void {
    const regionId = this.selectedRegionId();
    if (regionId == null || this.overriding()) return;
    this.overriding.set(true);
    this.service.removeOverrides(regionId, group.osmWayIds).subscribe({
      next: () => {
        this.overriding.set(false);
        if (this.selectedRegionId() === regionId) {
          const removed = new Set(group.osmWayIds);
          this.overrides.update((list) => list.filter((o) => !removed.has(o.osmWayId)));
        }
        this.snackBar.open(this.translateService.instant('STATS.NETWORK.OVERRIDE_QUEUED'), undefined, { duration: 4000 });
      },
      error: () => {
        this.overriding.set(false);
        this.snackBar.open(this.translateService.instant('STATS.NETWORK.OVERRIDE_FAILED'), undefined, { duration: 5000 });
        this.loadOverrides();
      },
    });
  }

  private loadOverrides(): void {
    this.overrides$.next(this.selectedRegionId());
  }

  /**
   * The page lives in a tab: the map is created, and fitted, while its container may still be
   * 0×0 (hidden tab, tab animation), which leaves Leaflet at a far-out zoom with stale tiles.
   * Tell Leaflet about every size change; refit only when the map becomes visible again, so a
   * later window resize keeps the user's view.
   */
  onMapReady(map: LeafletMap): void {
    const container = map.getContainer();
    let hidden = true;
    const observer = new ResizeObserver(() => {
      if (container.clientWidth === 0 || container.clientHeight === 0) {
        hidden = true;
        return;
      }
      map.invalidateSize();
      if (hidden) {
        hidden = false;
        map.fitBounds(this.bounds());
      }
    });
    observer.observe(container);
    this.destroyRef.onDestroy(() => observer.disconnect());
  }

  ngOnInit(): void {
    this.service.regions().subscribe({
      next: (regions) => {
        this.regions.set(regions);
        this.loading.set(false);
        if (regions.length > 0) {
          this.selectRegion(regions[0].regionId);
        }
      },
      error: () => {
        this.loading.set(false);
        this.snackBar.open(this.translateService.instant('STATS.NETWORK.LOAD_FAILED'), undefined, { duration: 5000 });
      },
    });
  }

  regionName(region: RegionNetworks): string {
    return this.translationService.getNameForItem(region);
  }

  selectRegion(regionId: number): void {
    this.selectedRegionId.set(regionId);
    this.ensureValidMode();
    this.selectedEdge.set(null);
    this.loadNetwork();
    this.loadOverrides();
  }

  selectMode(mode: NetworkMode): void {
    this.selectedMode.set(mode);
    this.selectedEdge.set(null);
    this.loadNetwork();
  }

  /**
   * Zoom to the edges of one missing run. For admins it also selects the run's longest edge for
   * curation: the keyboard path to the curation actions, which otherwise need a map click.
   */
  focusMissing(run: MissingRun): void {
    if (this.isAdmin) {
      const edges = (this.geometry()?.edges ?? []).filter((e) => run.edgeIds.includes(e.id));
      if (edges.length > 0) {
        this.selectedEdge.set(edges.reduce((a, b) => (b.lengthKm > a.lengthKm ? b : a)));
      }
    }
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
    this.selection$.next(this.selectedNetwork()?.id ?? null);
  }

  private buildLayers(geometry: NetworkGeometry, completion: NetworkCompletion, networkId: number): void {
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
        layer.on('click', () => this.selectedEdge.set(edge));
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
        line.on('click', () => this.selectedEdge.set(edge));
        overlays.push(line);
        this.edgeLayers.get(edge.id)?.push(line);
      }
    }

    this.layers.set([base, ...overlays]);
    // Fit only when another network is shown: a reload after curation must not throw the
    // admin out of the junction they were zoomed in on.
    if (this.fittedNetworkId !== networkId) {
      const bounds = this.mainBounds(geometry.edges);
      if (bounds.isValid()) {
        this.bounds.set(bounds);
        this.fittedNetworkId = networkId;
      }
    }
  }

  /**
   * The bounds of the network's main body. A region polygon can include far-flung territory —
   * the Netherlands' includes Aruba, whose tram is in the urban network — and fitting to all of
   * it shows half the globe. Edges more than 1,500 km from the median edge position are left
   * out of the fit (they are still drawn, a pan away).
   */
  private mainBounds(edges: NetworkGeometryEdge[]): LatLngBounds {
    const starts = edges.map((e) => this.toLatLngs(e.geometry)).filter((l) => l.length > 0);
    if (starts.length === 0) return new LatLngBounds([]);
    const median = (values: number[]) => values.sort((a, b) => a - b)[Math.floor(values.length / 2)];
    const center = latLng(median(starts.map((l) => l[0].lat)), median(starts.map((l) => l[0].lng)));
    const bounds = new LatLngBounds([]);
    for (const line of starts) {
      if (center.distanceTo(line[0]) <= 1_500_000) {
        for (const point of line) bounds.extend(point);
      }
    }
    return bounds;
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
