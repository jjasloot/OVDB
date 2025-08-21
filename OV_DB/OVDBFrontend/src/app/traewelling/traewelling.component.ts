import { Component, OnInit } from '@angular/core';
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
  template: `
    <div class="traewelling-container">
      <!-- Connection Status Card -->
      <mat-card class="connection-card" [ngClass]="{'connected': connectionStatus?.connected}">
        <mat-card-header>
          <mat-card-title class="connection-title">
            <mat-icon [class]="connectionStatus?.connected ? 'connected-icon' : 'disconnected-icon'">
              {{ connectionStatus?.connected ? 'link' : 'link_off' }}
            </mat-icon>
            {{ 'TRAEWELLING.TITLE' | translate }}
          </mat-card-title>
          <mat-card-subtitle *ngIf="connectionStatus?.user">
            {{ 'TRAEWELLING_CONNECTED' | translate }} {{ connectionStatus.user.displayName }}
          </mat-card-subtitle>
        </mat-card-header>
      </mat-card>

      <!-- Loading State -->
      <div *ngIf="isLoading" class="loading-container">
        <mat-spinner diameter="40"></mat-spinner>
        <p>{{ loadingMessage }}</p>
      </div>

      <!-- Empty State -->
      <mat-card *ngIf="!isLoading && trips.length === 0" class="empty-card">
        <mat-card-content class="empty-content">
          <mat-icon class="empty-icon">train</mat-icon>
          <h3>{{ 'TRAEWELLING.NO_UNIMPORTED_TRIPS_TITLE' | translate }}</h3>
          <p>{{ 'TRAEWELLING.NO_UNIMPORTED_TRIPS_MESSAGE' | translate }}</p>
        </mat-card-content>
      </mat-card>

      <!-- Trip Cards -->
      <div *ngIf="!isLoading && trips.length > 0" class="trips-container">
        <app-trip-card 
          *ngFor="let trip of trips; trackBy: trackTrip"
          [trip]="trip"
          (ignored)="onTripIgnored($event)"
          (linked)="onTripLinked($event)"
          (routeCreated)="onRouteCreated($event)">
        </app-trip-card>

        <!-- Load More Button -->
        <div *ngIf="hasMorePages" class="load-more-container">
          <button 
            mat-raised-button 
            color="primary" 
            (click)="loadMoreTrips()"
            [disabled]="isLoadingMore">
            <mat-icon *ngIf="isLoadingMore">hourglass_empty</mat-icon>
            {{ isLoadingMore ? ('TRAEWELLING.LOADING_MORE' | translate) : ('TRAEWELLING.LOAD_MORE' | translate) }}
          </button>
        </div>
      </div>
    </div>
  `,
  styleUrls: ['./traewelling.component.scss']
})
export class TrawellingComponent implements OnInit {
  connectionStatus: TrawellingConnectionStatus | null = null;
  trips: TrawellingTrip[] = [];
  isLoading = true;
  isLoadingMore = false;
  loadingMessage = 'Loading Träwelling trips...';
  hasMorePages = false;
  currentPage = 1;

  constructor(
    private trawellingService: TrawellingService,
    private snackBar: MatSnackBar
  ) {}

  async ngOnInit() {
    await this.loadConnectionStatus();
    if (this.connectionStatus?.connected) {
      await this.loadTrips();
    }
    this.isLoading = false;
  }

  trackTrip(index: number, trip: TrawellingTrip): number {
    return trip.id;
  }

  private async loadConnectionStatus() {
    try {
      this.connectionStatus = await this.trawellingService.getConnectionStatus();
    } catch (error) {
      console.error('Error loading connection status:', error);
      this.snackBar.open('Failed to check Träwelling connection', 'Close', { duration: 5000 });
    }
  }

  private async loadTrips() {
    try {
      const response = await this.trawellingService.getUnimportedTrips(1);
      this.trips = response.data;
      this.hasMorePages = response.hasMorePages;
      this.currentPage = 1;
    } catch (error) {
      console.error('Error loading trips:', error);
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
      this.currentPage = nextPage;
    } catch (error) {
      console.error('Error loading more trips:', error);
      this.snackBar.open('Failed to load more trips', 'Close', { duration: 5000 });
    } finally {
      this.isLoadingMore = false;
    }
  }

  onTripIgnored(tripId: number) {
    this.trips = this.trips.filter(trip => trip.id !== tripId);
    this.snackBar.open('Trip ignored successfully', 'Close', { duration: 3000 });
  }

  onTripLinked(tripId: number) {
    this.trips = this.trips.filter(trip => trip.id !== tripId);
    this.snackBar.open('Trip linked to route instance successfully', 'Close', { duration: 3000 });
  }

  onRouteCreated(tripId: number) {
    this.trips = this.trips.filter(trip => trip.id !== tripId);
    this.snackBar.open('Route created successfully', 'Close', { duration: 3000 });
  }
}