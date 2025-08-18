import { Component, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { MatCardModule } from '@angular/material/card';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';
import { MatProgressSpinnerModule } from '@angular/material/progress-spinner';
import { TranslateModule } from '@ngx-translate/core';
import { TripCardComponent } from '../trip-card/trip-card.component';
import { TrawellingService } from '../../services/traewelling.service';
import { TrawellingTrip, TrawellingTripsResponse } from '../../../models/traewelling.model';

@Component({
  selector: 'app-trip-list',
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
  template: `
    <div class="trip-list-container">
      <!-- Header Actions -->
      <mat-card class="actions-card">
        <mat-card-content>
          <div class="actions-header">
            <mat-card-title>{{ 'TRAEWELLING.UNIMPORTED_TRIPS' | translate }}</mat-card-title>
            <button 
              mat-raised-button 
              color="primary" 
              (click)="refreshTrips()" 
              [disabled]="loading">
              <mat-icon>refresh</mat-icon>
              {{ 'TRAEWELLING.REFRESH' | translate }}
            </button>
          </div>
        </mat-card-content>
      </mat-card>

      <!-- Loading State -->
      @if (loading && trips.length === 0) {
        <div class="loading-container">
          <mat-spinner></mat-spinner>
          <span>{{ 'TRAEWELLING.LOADING_TRIPS' | translate }}</span>
        </div>
      }

      <!-- No Trips State -->
      @else if (!loading && trips.length === 0) {
        <mat-card class="no-trips-card">
          <mat-card-content>
            <div class="no-trips-content">
              <mat-icon>check_circle</mat-icon>
              <h3>{{ 'TRAEWELLING.NO_TRIPS' | translate }}</h3>
              <p>{{ 'TRAEWELLING.NO_UNIMPORTED_TRIPS_MESSAGE' | translate }}</p>
            </div>
          </mat-card-content>
        </mat-card>
      }

      <!-- Trips List -->
      @else {
        <div class="trips-grid">
          @for (trip of trips; track trip.id) {
            <app-trip-card 
              [trip]="trip"
              (actionPerformed)="handleTripAction($event)">
            </app-trip-card>
          }
        </div>

        <!-- Load More -->
        @if (hasMorePages) {
          <div class="load-more-container">
            @if (loadingMore) {
              <div class="loading-more">
                <mat-spinner diameter="24"></mat-spinner>
                <span>{{ 'TRAEWELLING.LOADING_MORE_TRIPS' | translate }}</span>
              </div>
            } @else {
              <button 
                mat-raised-button 
                color="primary" 
                (click)="loadMoreTrips()"
                [disabled]="loading">
                <mat-icon>more_horiz</mat-icon>
                {{ 'TRAEWELLING.LOAD_MORE' | translate }}
              </button>
            }
          </div>
        }
      }
    </div>
  `,
  styles: [`
    .trip-list-container {
      display: flex;
      flex-direction: column;
      gap: 24px;
    }

    .actions-card {
      margin-bottom: 8px;
    }

    .actions-header {
      display: flex;
      justify-content: space-between;
      align-items: center;
      flex-wrap: wrap;
      gap: 16px;
    }

    .actions-header mat-card-title {
      margin: 0;
      font-size: 20px;
      font-weight: 500;
    }

    .loading-container,
    .loading-more {
      display: flex;
      flex-direction: column;
      align-items: center;
      justify-content: center;
      gap: 16px;
      padding: 48px 16px;
    }

    .loading-more {
      padding: 24px 16px;
    }

    .no-trips-card {
      margin-top: 24px;
    }

    .no-trips-content {
      display: flex;
      flex-direction: column;
      align-items: center;
      gap: 16px;
      text-align: center;
      padding: 48px 16px;
    }

    .no-trips-content mat-icon {
      font-size: 64px;
      width: 64px;
      height: 64px;
      color: #4caf50;
    }

    .no-trips-content h3 {
      margin: 0;
      font-size: 20px;
      font-weight: 500;
    }

    .no-trips-content p {
      margin: 0;
      opacity: 0.7;
      max-width: 400px;
    }

    .trips-grid {
      display: grid;
      gap: 24px;
      grid-template-columns: repeat(auto-fit, minmax(400px, 1fr));
    }

    .load-more-container {
      display: flex;
      justify-content: center;
      margin-top: 16px;
    }

    /* Mobile responsiveness */
    @media (max-width: 768px) {
      .trips-grid {
        grid-template-columns: 1fr;
        gap: 16px;
      }

      .actions-header {
        flex-direction: column;
        align-items: stretch;
      }

      .actions-header button {
        width: 100%;
      }
    }

    @media (max-width: 480px) {
      .trip-list-container {
        gap: 16px;
      }

      .trips-grid {
        gap: 12px;
      }
    }
  `]
})
export class TripListComponent implements OnInit {
  trips: TrawellingTrip[] = [];
  loading = false;
  loadingMore = false;
  hasMorePages = false;
  currentPage = 1;

  constructor(private trawellingService: TrawellingService) {}

  async ngOnInit() {
    await this.loadTrips();
  }

  async refreshTrips() {
    this.trips = [];
    this.currentPage = 1;
    this.hasMorePages = false;
    await this.loadTrips();
  }

  async loadTrips() {
    this.loading = true;
    try {
      const response = await this.trawellingService.getUnimportedTrips(this.currentPage);
      this.trips = [...this.trips, ...response.data];
      this.hasMorePages = response.hasMorePages;
    } catch (error) {
      console.error('Error loading trips:', error);
    } finally {
      this.loading = false;
    }
  }

  async loadMoreTrips() {
    if (this.loadingMore || !this.hasMorePages) return;
    
    this.loadingMore = true;
    this.currentPage++;
    
    try {
      const response = await this.trawellingService.getUnimportedTrips(this.currentPage);
      this.trips = [...this.trips, ...response.data];
      this.hasMorePages = response.hasMorePages;
    } catch (error) {
      console.error('Error loading more trips:', error);
      this.currentPage--; // Revert page increment on error
    } finally {
      this.loadingMore = false;
    }
  }

  handleTripAction(event: { action: string; trip: TrawellingTrip; data?: any }) {
    // Handle trip actions from child components
    console.log('Trip action:', event);
    
    // Remove trip from list if it was successfully processed
    if (event.action === 'linked' || event.action === 'ignored') {
      this.trips = this.trips.filter(trip => trip.id !== event.trip.id);
    }
  }
}