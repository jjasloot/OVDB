import { Component, OnInit, inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { MatCardModule } from '@angular/material/card';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';
import { MatProgressSpinnerModule } from '@angular/material/progress-spinner';
import { MatSnackBar } from '@angular/material/snack-bar';
import { TranslateModule } from '@ngx-translate/core';
import { TrawellingService } from './services/traewelling.service';
import {
  TrawellingConnectionStatus,
  TrawellingTripsResponse,
  TrawellingTrip
} from '../models/traewelling.model';
import { TripCardComponent } from './components/trip-card/trip-card.component';

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
    TripCardComponent
  ],
  templateUrl: './traewelling.component.html',
  styleUrls: ['./traewelling.component.scss']
})
export class TrawellingComponent implements OnInit {
  private trawellingService = inject(TrawellingService);
  private snackBar = inject(MatSnackBar);

  connectionStatus: TrawellingConnectionStatus | null = null;
  trips: TrawellingTrip[] = [];
  isLoading = true;
  isLoadingMore = false;
  hasMorePages = false;
  currentPage = 1;

  async ngOnInit() {
    await this.loadConnectionStatus();
    if (this.connectionStatus?.connected) {
      await this.loadTrips();
    }
    this.isLoading = false;
  }

  removeTrip(tripId: number): void {
    this.trips = this.trips.filter(trip => trip.id !== tripId);
  }

  private async loadConnectionStatus() {
    try {
      this.connectionStatus = await this.trawellingService.getConnectionStatus();
    } catch (error) {
      this.snackBar.open('Failed to check Träwelling connection', 'Close', { duration: 5000 });
    }
  }

  private async loadTrips() {
    try {
      const response = await this.trawellingService.getUnimportedTrips(1);
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