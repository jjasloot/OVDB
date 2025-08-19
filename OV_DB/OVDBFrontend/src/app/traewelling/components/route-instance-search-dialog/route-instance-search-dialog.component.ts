import { Component, Inject, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { MatDialogModule, MatDialogRef, MAT_DIALOG_DATA } from '@angular/material/dialog';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';
import { MatProgressSpinnerModule } from '@angular/material/progress-spinner';
import { MatListModule } from '@angular/material/list';
import { MatDividerModule } from '@angular/material/divider';
import { MatSnackBar } from '@angular/material/snack-bar';
import { TrawellingService } from '../../services/traewelling.service';
import { TrawellingTrip, RouteInstanceSearchResult } from '../../../models/traewelling.model';

@Component({
  selector: 'app-route-instance-search-dialog',
  standalone: true,
  imports: [
    CommonModule,
    FormsModule,
    MatDialogModule,
    MatButtonModule,
    MatIconModule,
    MatProgressSpinnerModule,
    MatListModule,
    MatDividerModule
  ],
  template: `
    <h2 mat-dialog-title>Connect to Existing RouteInstance</h2>
    
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

      <!-- Search Results -->
      <div class="search-section">
        <h4>Matching Route Instances</h4>
        
        <!-- Loading State -->
        <div *ngIf="isLoading" class="loading-container">
          <mat-spinner diameter="30"></mat-spinner>
          <p>Searching for matching route instances...</p>
        </div>

        <!-- Empty State -->
        <div *ngIf="!isLoading && routeInstances.length === 0" class="empty-container">
          <mat-icon class="empty-icon">search_off</mat-icon>
          <p>No matching route instances found for this trip.</p>
          <p class="empty-hint">Try adjusting the search criteria or create a new route instead.</p>
        </div>

        <!-- Results List -->
        <mat-selection-list 
          *ngIf="!isLoading && routeInstances.length > 0"
          [(ngModel)]="selectedRouteInstance"
          [multiple]="false">
          <mat-list-option 
            *ngFor="let instance of routeInstances" 
            [value]="instance"
            class="route-instance-option">
            <div class="instance-info">
              <div class="instance-header">
                <strong>{{ instance.routeName }}</strong>
                <span class="instance-date">{{ formatInstanceDate(instance.date) }}</span>
              </div>
              <div class="instance-route">
                {{ instance.from }} → {{ instance.to }}
              </div>
              <div class="instance-times" *ngIf="instance.startTime && instance.endTime">
                <mat-icon>schedule</mat-icon>
                {{ instance.startTime }} - {{ instance.endTime }}
                <span *ngIf="instance.durationHours" class="duration">
                  ({{ instance.durationHours }}h)
                </span>
              </div>
              <div *ngIf="instance.hasTraewellingLink" class="already-linked">
                <mat-icon>link</mat-icon>
                Already linked to Träwelling
              </div>
            </div>
          </mat-list-option>
        </mat-selection-list>
      </div>
    </mat-dialog-content>

    <mat-dialog-actions align="end">
      <button mat-button (click)="onCancel()">Cancel</button>
      <button 
        mat-raised-button 
        color="primary" 
        (click)="onLink()"
        [disabled]="!selectedRouteInstance || isLinking">
        <mat-spinner *ngIf="isLinking" diameter="16" style="margin-right: 8px;"></mat-spinner>
        {{ isLinking ? 'Linking...' : 'Link to RouteInstance' }}
      </button>
    </mat-dialog-actions>
  `,
  styleUrls: ['./route-instance-search-dialog.component.scss']
})
export class RouteInstanceSearchDialogComponent implements OnInit {
  routeInstances: RouteInstanceSearchResult[] = [];
  selectedRouteInstance: RouteInstanceSearchResult | null = null;
  isLoading = true;
  isLinking = false;

  constructor(
    public trawellingService: TrawellingService,
    private snackBar: MatSnackBar,
    private dialogRef: MatDialogRef<RouteInstanceSearchDialogComponent>,
    @Inject(MAT_DIALOG_DATA) public data: { trip: TrawellingTrip }
  ) {}

  async ngOnInit() {
    await this.searchRouteInstances();
  }

  getJourneyNumber(): string {
    return this.data.trip.transport.manualJourneyNumber || 
           this.data.trip.transport.journeyNumber?.toString() || '';
  }

  formatInstanceDate(dateString: string): string {
    return this.trawellingService.formatDate(dateString);
  }

  private async searchRouteInstances() {
    try {
      this.routeInstances = await this.trawellingService.searchRouteInstances(this.data.trip);
    } catch (error) {
      console.error('Error searching route instances:', error);
      this.snackBar.open('Failed to search route instances', 'Close', { duration: 5000 });
    } finally {
      this.isLoading = false;
    }
  }

  async onLink() {
    if (!this.selectedRouteInstance) return;

    this.isLinking = true;
    try {
      const response = await this.trawellingService.linkToRouteInstance(
        this.data.trip.id, 
        this.selectedRouteInstance.id
      );
      
      if (response.success) {
        this.snackBar.open('Successfully linked to route instance', 'Close', { duration: 3000 });
        this.dialogRef.close({ success: true, routeInstance: response.routeInstance });
      } else {
        this.snackBar.open(response.message || 'Failed to link to route instance', 'Close', { duration: 5000 });
      }
    } catch (error) {
      console.error('Error linking to route instance:', error);
      this.snackBar.open('Failed to link to route instance', 'Close', { duration: 5000 });
    } finally {
      this.isLinking = false;
    }
  }

  onCancel() {
    this.dialogRef.close();
  }
}