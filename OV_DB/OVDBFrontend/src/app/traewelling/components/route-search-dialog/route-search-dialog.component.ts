import { Component, Inject, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { MatDialogModule, MatDialogRef, MAT_DIALOG_DATA } from '@angular/material/dialog';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatInputModule } from '@angular/material/input';
import { MatProgressSpinnerModule } from '@angular/material/progress-spinner';
import { MatListModule } from '@angular/material/list';
import { MatDividerModule } from '@angular/material/divider';
import { MatSnackBar } from '@angular/material/snack-bar';
import { Router } from '@angular/router';
import { debounceTime, distinctUntilChanged } from 'rxjs/operators';
import { Subject } from 'rxjs';
import { TrawellingService } from '../../services/traewelling.service';
import { TrawellingTrip, RouteSearchResult } from '../../../models/traewelling.model';

@Component({
  selector: 'app-route-search-dialog',
  standalone: true,
  imports: [
    CommonModule,
    FormsModule,
    MatDialogModule,
    MatButtonModule,
    MatIconModule,
    MatFormFieldModule,
    MatInputModule,
    MatProgressSpinnerModule,
    MatListModule,
    MatDividerModule
  ],
  template: `
    <h2 mat-dialog-title>Add to Existing Route</h2>
    
    <mat-dialog-content class="dialog-content">
      <!-- Trip Summary -->
      <div class="trip-summary">
        <h4>Trip Details</h4>
        <div class="trip-info">
          <div class="route-line">
            <strong>{{ data.trip.transport.lineName }}</strong>
            <span *ngIf="getJourneyNumber()"> • {{ getJourneyNumber() }}</span>
          </div>
          <div class="route-stations">
            {{ data.trip.transport.origin.name }} → {{ data.trip.transport.destination.name }}
          </div>
          <div class="route-times">
            {{ trawellingService.formatTime(data.trip.transport.origin.departureScheduled) }} - 
            {{ trawellingService.formatTime(data.trip.transport.destination.arrivalScheduled) }}
          </div>
        </div>
      </div>

      <mat-divider></mat-divider>

      <!-- Search Section -->
      <div class="search-section">
        <h4>Search for Route</h4>
        
        <mat-form-field class="search-field" appearance="outline">
          <mat-label>Route name, city, or station</mat-label>
          <input 
            matInput 
            [(ngModel)]="searchQuery" 
            (input)="onSearchInput()"
            placeholder="e.g. Amsterdam, NS380, Hengelo">
          <mat-icon matSuffix>search</mat-icon>
        </mat-form-field>

        <!-- Loading State -->
        <div *ngIf="isSearching" class="loading-container">
          <mat-spinner diameter="30"></mat-spinner>
          <p>Searching routes...</p>
        </div>

        <!-- Empty State -->
        <div *ngIf="!isSearching && searchQuery && routes.length === 0" class="empty-container">
          <mat-icon class="empty-icon">search_off</mat-icon>
          <p>No routes found matching "{{ searchQuery }}"</p>
          <p class="empty-hint">Try searching with different keywords or city names.</p>
        </div>

        <!-- Results List -->
        <mat-selection-list 
          *ngIf="!isSearching && routes.length > 0"
          [(ngModel)]="selectedRoute"
          [multiple]="false">
          <mat-list-option 
            *ngFor="let route of routes" 
            [value]="route"
            class="route-option">
            <div class="route-info">
              <div class="route-header">
                <strong>{{ route.name }}</strong>
                <span class="route-type">{{ route.type }}</span>
              </div>
              <div class="route-path">
                {{ route.from }} → {{ route.to }}
              </div>
              <div class="route-country">
                <mat-icon>place</mat-icon>
                {{ route.country }}
              </div>
            </div>
          </mat-list-option>
        </mat-selection-list>

        <!-- Search Hint -->
        <div *ngIf="!searchQuery" class="search-hint">
          <mat-icon>info</mat-icon>
          <p>Enter a route name, city, or station name to search for existing routes.</p>
        </div>
      </div>
    </mat-dialog-content>

    <mat-dialog-actions align="end">
      <button mat-button (click)="onCancel()">Cancel</button>
      <button 
        mat-raised-button 
        color="primary" 
        (click)="onSelectRoute()"
        [disabled]="!selectedRoute || isCreating">
        <mat-spinner *ngIf="isCreating" diameter="16" style="margin-right: 8px;"></mat-spinner>
        {{ isCreating ? 'Adding...' : 'Add to Route' }}
      </button>
    </mat-dialog-actions>
  `,
  styleUrls: ['./route-search-dialog.component.scss']
})
export class RouteSearchDialogComponent implements OnInit {
  searchQuery = '';
  routes: RouteSearchResult[] = [];
  selectedRoute: RouteSearchResult | null = null;
  isSearching = false;
  isCreating = false;
  
  private searchSubject = new Subject<string>();

  constructor(
    public trawellingService: TrawellingService,
    private snackBar: MatSnackBar,
    private router: Router,
    private dialogRef: MatDialogRef<RouteSearchDialogComponent>,
    @Inject(MAT_DIALOG_DATA) public data: { trip: TrawellingTrip }
  ) {}

  ngOnInit() {
    // Set up debounced search
    this.searchSubject.pipe(
      debounceTime(300),
      distinctUntilChanged()
    ).subscribe((query: string) => {
      if (query.trim()) {
        this.performSearch(query.trim());
      } else {
        this.routes = [];
      }
    });
    
    // Initialize search with trip origin/destination
    this.searchQuery = this.data.trip.transport.origin.name;
    this.onSearchInput();
  }

  getJourneyNumber(): string {
    return this.data.trip.transport.manualJourneyNumber || 
           this.data.trip.transport.journeyNumber?.toString() || '';
  }

  onSearchInput() {
    this.searchSubject.next(this.searchQuery);
  }

  private async performSearch(query: string) {
    this.isSearching = true;
    try {
      this.routes = await this.trawellingService.searchRoutes(query);
    } catch (error) {
      console.error('Error searching routes:', error);
      this.snackBar.open('Failed to search routes', 'Close', { duration: 5000 });
    } finally {
      this.isSearching = false;
    }
  }

  async onSelectRoute() {
    if (!this.selectedRoute) return;

    this.isCreating = true;
    try {
      // Store trip context and navigate to route instances page
      const tripContext = this.trawellingService.getTripContextForRouteCreation(this.data.trip);
      sessionStorage.setItem('trawellingTripContext', JSON.stringify(tripContext));
      
      // Close dialog and navigate
      this.dialogRef.close({ routeCreated: true });
      this.router.navigate(['/route-instances'], { 
        queryParams: { 
          routeId: this.selectedRoute.id,
          fromTraewelling: 'true'
        }
      });
    } catch (error) {
      console.error('Error navigating to route:', error);
      this.snackBar.open('Failed to navigate to route', 'Close', { duration: 5000 });
    } finally {
      this.isCreating = false;
    }
  }

  onCancel() {
    this.dialogRef.close();
  }
}