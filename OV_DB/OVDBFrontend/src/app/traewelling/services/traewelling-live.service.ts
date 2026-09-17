import { Injectable, inject } from '@angular/core';
import { HubConnection, HubConnectionBuilder } from '@microsoft/signalr';
import { Subject } from 'rxjs';
import { environment } from 'src/environments/environment';
import { AuthenticationService } from '../../services/authentication.service';
import {
  TrawellingConflict,
  TrawellingResyncProgress,
  TrawellingResyncResult,
  TrawellingTrip
} from '../../models/traewelling.model';

/**
 * Live updates for the unimported-trips list, pushed by the backend when Träwelling
 * webhook events arrive and while a full-history resync runs. The hub is JWT-authenticated
 * and only delivers the current user's own events. Payloads are JSON strings shaped exactly
 * like the REST responses.
 */
@Injectable({ providedIn: 'root' })
export class TraewellingLiveService {
  private authService = inject(AuthenticationService);
  private connection?: HubConnection;

  tripUpserted$ = new Subject<TrawellingTrip>();
  tripRemoved$ = new Subject<number>();
  conflictUpserted$ = new Subject<TrawellingConflict>();
  conflictRemoved$ = new Subject<number>();
  resyncProgress$ = new Subject<TrawellingResyncProgress>();
  resyncFinished$ = new Subject<TrawellingResyncResult>();

  connect(): void {
    if (this.connection) {
      return;
    }
    const connection = new HubConnectionBuilder()
      .withUrl(environment.backend + 'traewellingHub', {
        accessTokenFactory: () => this.authService.token ?? '',
      })
      .withAutomaticReconnect()
      .build();
    this.connection = connection;

    connection.on('PendingTripUpserted', (tripJson: string) => {
      try {
        this.tripUpserted$.next(JSON.parse(tripJson) as TrawellingTrip);
      } catch (err) {
        console.error('Could not parse live Träwelling trip payload', err);
      }
    });
    connection.on('PendingTripRemoved', (statusId: number) => {
      this.tripRemoved$.next(statusId);
    });
    connection.on('ConflictUpserted', (conflictJson: string) => {
      try {
        this.conflictUpserted$.next(JSON.parse(conflictJson) as TrawellingConflict);
      } catch (err) {
        console.error('Could not parse live Träwelling conflict payload', err);
      }
    });
    connection.on('ConflictRemoved', (statusId: number) => {
      this.conflictRemoved$.next(statusId);
    });
    connection.on('ResyncProgress', (progressJson: string) => {
      try {
        this.resyncProgress$.next(JSON.parse(progressJson) as TrawellingResyncProgress);
      } catch (err) {
        console.error('Could not parse Träwelling resync progress payload', err);
      }
    });
    connection.on('ResyncFinished', (resultJson: string) => {
      try {
        this.resyncFinished$.next(JSON.parse(resultJson) as TrawellingResyncResult);
      } catch (err) {
        console.error('Could not parse Träwelling resync result payload', err);
      }
    });

    connection
      .start()
      .catch((err) => console.error('Träwelling live connection failed', err));
  }

  disconnect(): void {
    this.connection?.stop();
    this.connection = undefined;
  }
}
