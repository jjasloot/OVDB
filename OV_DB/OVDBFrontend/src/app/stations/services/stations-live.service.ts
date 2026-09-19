import { Injectable, inject } from '@angular/core';
import { HubConnection, HubConnectionBuilder } from '@microsoft/signalr';
import { Subject } from 'rxjs';
import { environment } from 'src/environments/environment';
import { AuthenticationService } from '../../services/authentication.service';
import {
  BackfillWarmupProgress,
  BackfillWarmupResult,
} from '../../models/stationView.model';

/**
 * Live updates for the station screens. Today that is one thing: the route index the backfill
 * queue runs on being built, which takes seconds and outlives the request that asked for it.
 * The hub is JWT-authenticated and only delivers the current user's own events.
 */
@Injectable({ providedIn: 'root' })
export class StationsLiveService {
  private authService = inject(AuthenticationService);
  private connection?: HubConnection;
  private started?: Promise<void>;

  warmupProgress$ = new Subject<BackfillWarmupProgress>();
  warmupFinished$ = new Subject<BackfillWarmupResult>();

  /**
   * Resolves once the connection is up, so a caller can be listening before it asks for work that
   * reports here. Queueing first and connecting after loses the event of a build that finishes in
   * the meantime, and the page would then wait for something already done.
   */
  connect(): Promise<void> {
    if (this.started) {
      return this.started;
    }
    const connection = new HubConnectionBuilder()
      .withUrl(environment.backend + 'stationsHub', {
        accessTokenFactory: () => this.authService.token ?? '',
      })
      .withAutomaticReconnect()
      .build();
    this.connection = connection;

    connection.on('BackfillWarmupProgress', (progressJson: string) => {
      try {
        this.warmupProgress$.next(JSON.parse(progressJson) as BackfillWarmupProgress);
      } catch (err) {
        console.error('Could not parse backfill warm-up progress payload', err);
      }
    });
    connection.on('BackfillWarmupFinished', (resultJson: string) => {
      try {
        this.warmupFinished$.next(JSON.parse(resultJson) as BackfillWarmupResult);
      } catch (err) {
        console.error('Could not parse backfill warm-up result payload', err);
      }
    });

    this.started = connection.start();
    return this.started;
  }

  disconnect(): void {
    this.connection?.stop();
    this.connection = undefined;
    this.started = undefined;
  }
}
