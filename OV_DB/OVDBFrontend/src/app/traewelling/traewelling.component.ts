import { Component, OnInit, OnDestroy, inject, ChangeDetectionStrategy } from '@angular/core';
import { Subscription, firstValueFrom } from 'rxjs';
import { CommonModule } from '@angular/common';
import { MatCardModule } from '@angular/material/card';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';
import { MatProgressSpinnerModule } from '@angular/material/progress-spinner';
import { MatSnackBar } from '@angular/material/snack-bar';
import { TranslateModule, TranslateService } from '@ngx-translate/core';
import { MatDialog } from '@angular/material/dialog';
import { first } from 'rxjs/operators';
import { TrawellingService } from './services/traewelling.service';
import { TraewellingLiveService } from './services/traewelling-live.service';
import {
  TrawellingConnectionStatus,
  TrawellingTripsResponse,
  TrawellingTrip,
  TrawellingConflict,
  TrawellingConflictAction,
  TraewellingAlert,
  TraewellingAlertTranslation
} from '../models/traewelling.model';
import { TripCardComponent } from './components/trip-card/trip-card.component';
import { ConflictCardComponent } from './components/conflict-card/conflict-card.component';
import { AreYouSureDialogComponent } from '../are-you-sure-dialog/are-you-sure-dialog.component';
import { STANDARD_DIALOG } from '../constants/dialog-sizes';

@Component({
  selector: 'app-traewelling',
  standalone: true,
  imports: [
    CommonModule,
    MatCardModule,
    MatButtonModule,
    MatIconModule,
    MatProgressSpinnerModule,
    TranslateModule,
    TripCardComponent,
    ConflictCardComponent
  ],
  templateUrl: './traewelling.component.html',
  changeDetection: ChangeDetectionStrategy.Eager,
  styleUrls: ['./traewelling.component.scss']
})
export class TrawellingComponent implements OnInit, OnDestroy {
  private trawellingService = inject(TrawellingService);
  private liveService = inject(TraewellingLiveService);
  private snackBar = inject(MatSnackBar);
  private translateService = inject(TranslateService);
  private dialog = inject(MatDialog);

  connectionStatus: TrawellingConnectionStatus | null = null;
  trips: TrawellingTrip[] = [];
  conflicts: TrawellingConflict[] = [];
  alerts: TraewellingAlert[] = [];
  isLoading = true;
  isLoadingMore = false;
  hasMorePages = false;
  currentPage = 1;
  conflictBusy = false;
  private liveSubscriptions: Subscription[] = [];

  async ngOnInit() {
    await this.loadConnectionStatus();
    if (this.connectionStatus?.connected) {
      await Promise.all([this.loadTrips(), this.loadAlerts(), this.loadConflicts()]);
      if (this.connectionStatus.liveSyncEnabled) {
        this.startLiveUpdates();
      }
    }
    this.isLoading = false;
  }

  ngOnDestroy(): void {
    this.liveSubscriptions.forEach(s => s.unsubscribe());
    this.liveSubscriptions = [];
    this.liveService.disconnect();
  }

  removeTrip(tripId: number): void {
    this.trips = this.trips.filter(trip => trip.id !== tripId);
  }

  private startLiveUpdates(): void {
    this.liveService.connect();
    this.liveSubscriptions.push(
      this.liveService.tripUpserted$.subscribe(trip => this.upsertTrip(trip)),
      this.liveService.tripRemoved$.subscribe(statusId => this.removeTrip(statusId)),
      this.liveService.conflictUpserted$.subscribe(conflict => this.upsertConflict(conflict)),
    );
  }

  private upsertConflict(conflict: TrawellingConflict): void {
    const index = this.conflicts.findIndex(c => c.statusId === conflict.statusId);
    if (index >= 0) {
      const updated = [...this.conflicts];
      updated[index] = conflict;
      this.conflicts = updated;
    } else {
      this.conflicts = [conflict, ...this.conflicts];
    }
  }

  private upsertTrip(trip: TrawellingTrip): void {
    const index = this.trips.findIndex(t => t.id === trip.id);
    if (index >= 0) {
      const updated = [...this.trips];
      updated[index] = trip;
      this.trips = updated;
    } else {
      // The list is ordered newest departure first, and a live check-in is the newest
      this.trips = [trip, ...this.trips];
    }
  }

  getAlertTranslation(alert: TraewellingAlert): TraewellingAlertTranslation {
    const lang = this.translateService.currentLang || 'en';
    return alert.translations?.find(t => t.locale === lang)
      ?? alert.translations?.find(t => t.locale === 'en')
      ?? alert.translations?.[0]
      ?? { title: '', content: '', locale: 'en' };
  }

  private async loadConnectionStatus() {
    try {
      this.connectionStatus = await this.trawellingService.getConnectionStatus();
    } catch (error) {
      this.snackBar.open('Failed to check Träwelling connection', 'Close', { duration: 5000 });
    }
  }

  private async loadAlerts() {
    try {
      this.alerts = await this.trawellingService.getAlerts() ?? [];
    } catch {
      // Alerts are non-critical; ignore errors silently
    }
  }

  private async loadConflicts() {
    try {
      this.conflicts = await this.trawellingService.getConflicts() ?? [];
    } catch {
      // The conflicts section simply stays empty; the global error toast already fired
    }
  }

  async resolveConflict(conflict: TrawellingConflict, action: TrawellingConflictAction) {
    if (this.conflictBusy) return;

    if (action === 'delete-instance') {
      // When this is the route's only trip, the backend removes the route as well —
      // say so before asking for confirmation
      const confirmKey = conflict.isLastInstanceOnRoute
        ? 'TRAEWELLING.CONFLICT_DELETE_CONFIRM_WITH_ROUTE'
        : 'TRAEWELLING.CONFLICT_DELETE_CONFIRM';
      const dialogRef = this.dialog.open(AreYouSureDialogComponent, {
        ...STANDARD_DIALOG,
        data: { item: this.translateService.instant(confirmKey) },
      });
      const confirmed = await firstValueFrom(dialogRef.afterClosed().pipe(first()));
      if (!confirmed) return;
    }

    this.conflictBusy = true;
    try {
      const success = await this.trawellingService.resolveConflict(conflict.statusId, action);
      if (success) {
        this.conflicts = this.conflicts.filter(c => c.statusId !== conflict.statusId);
        if (action === 'reimport') {
          // The status is pending again — show it in the list below
          await this.loadTrips();
        }
        const messageKey = {
          'apply-times': 'TRAEWELLING.CONFLICT_APPLIED',
          'reimport': 'TRAEWELLING.CONFLICT_REIMPORTED',
          'dismiss': 'TRAEWELLING.CONFLICT_DISMISSED',
          'delete-instance': 'TRAEWELLING.CONFLICT_INSTANCE_DELETED',
        }[action];
        this.snackBar.open(this.translateService.instant(messageKey), this.translateService.instant('CLOSE'), { duration: 4000 });
      }
    } catch {
      // The global error interceptor already showed a toast
    } finally {
      this.conflictBusy = false;
    }
  }

  async refreshTrips() {
    if (this.isLoading) return;
    this.isLoading = true;
    try {
      await this.loadTrips(true);
    } finally {
      this.isLoading = false;
    }
  }

  private async loadTrips(refresh = false) {
    try {
      const response = await this.trawellingService.getUnimportedTrips(1, refresh);
      this.trips = response.data;
      this.hasMorePages = response.hasMorePages;
      this.currentPage = response.meta.current_page;
    } catch (error) {
      this.snackBar.open('Failed to load trips', 'Close', { duration: 5000 });
    }
  }

  async loadMoreTrips() {
    if (this.isLoadingMore || !this.hasMorePages) return;

    this.isLoadingMore = true;
    try {
      const nextPage = this.currentPage + 1;
      const response = await this.trawellingService.getUnimportedTrips(nextPage);

      this.trips = [...this.trips, ...response.data];
      this.hasMorePages = response.hasMorePages;
      this.currentPage = response.meta.current_page;
    } catch (error) {
      this.snackBar.open('Failed to load more trips', 'Close', { duration: 5000 });
    } finally {
      this.isLoadingMore = false;
    }
  }

}