import { HttpClient, HttpParams } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable } from 'rxjs';
import { environment } from 'src/environments/environment';
import {
  MissingRun,
  NetworkCompletion,
  NetworkGeometry,
  NetworkStatusRow,
  RegionNetworks,
} from '../models/network-completion.model';

/** Region rail network completion: see docs/region-network-completion.md. */
@Injectable({ providedIn: 'root' })
export class NetworkCompletionService {
  private httpClient = inject(HttpClient);
  private base = environment.backend + 'api/networks/';

  regions(): Observable<RegionNetworks[]> {
    return this.httpClient.get<RegionNetworks[]>(this.base + 'regions');
  }

  /** User-independent; the browser revalidates it by ETag, so repeat loads are cheap. */
  geometry(networkId: number): Observable<NetworkGeometry> {
    return this.httpClient.get<NetworkGeometry>(this.base + networkId + '/geometry');
  }

  completion(networkId: number, from?: Date | null, to?: Date | null): Observable<NetworkCompletion> {
    return this.httpClient.get<NetworkCompletion>(this.base + networkId + '/completion', { params: this.range(from, to) });
  }

  missing(networkId: number, from?: Date | null, to?: Date | null): Observable<MissingRun[]> {
    return this.httpClient.get<MissingRun[]>(this.base + networkId + '/missing', { params: this.range(from, to) });
  }

  status(regionId: number): Observable<NetworkStatusRow[]> {
    return this.httpClient.get<NetworkStatusRow[]>(this.base + 'region/' + regionId + '/status');
  }

  assemble(regionId: number, refresh = false): Observable<void> {
    return this.httpClient.post<void>(this.base + 'region/' + regionId + '/assemble', {}, { params: { refresh } });
  }

  rematch(regionId: number): Observable<void> {
    return this.httpClient.post<void>(this.base + 'region/' + regionId + '/rematch', {});
  }

  private range(from?: Date | null, to?: Date | null): HttpParams {
    let params = new HttpParams();
    if (from) params = params.set('from', from.toISOString());
    if (to) params = params.set('to', to.toISOString());
    return params;
  }
}
