import { Component, Input, Output, EventEmitter } from '@angular/core';
import { CommonModule } from '@angular/common';
import { MatCardModule } from '@angular/material/card';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';
import { MatChipsModule } from '@angular/material/chips';
import { MatMenuModule } from '@angular/material/menu';
import { MatProgressSpinnerModule } from '@angular/material/progress-spinner';
import { Router } from '@angular/router';
import { TrawellingTrip } from '../../../models/traewelling.model';
import { TrawellingService } from '../../services/traewelling.service';
import { RouteInstanceSearchDialogComponent } from '../route-instance-search-dialog/route-instance-search-dialog.component';
import { RouteSearchDialogComponent } from '../route-search-dialog/route-search-dialog.component';
import { MatDialog } from '@angular/material/dialog';

@Component({
  selector: 'app-trip-card',
  standalone: true,
  imports: [
    CommonModule,
    MatCardModule,
    MatButtonModule,
    MatIconModule,
    MatChipsModule,
    MatMenuModule,
    MatProgressSpinnerModule
  ],
  template: `
    <mat-card class="trip-card">
      <!-- Trip Header -->
      <mat-card-header class="trip-header">
        <div class="transport-info">
          <!-- Transport Icon -->
          <div class="transport-icon" [style.background-color]="getTransportColor()">
            <mat-icon>{{ getTransportIcon() }}</mat-icon>
          </div>
          
          <!-- Line and Journey Numbers -->
          <div class="line-info">
            <div class="line-number">{{ trip.transport.lineName }}</div>
            <div *ngIf="getJourneyNumber()" class="journey-number">
              {{ getJourneyNumber() }}
            </div>
          </div>
        </div>

        <!-- Date -->
        <div class="trip-date">
          <mat-icon>calendar_today</mat-icon>
          {{ trawellingService.formatDate(trip.createdAt) }}
        </div>
      </mat-card-header>

      <!-- Route Information -->
      <mat-card-content class="route-info">
        <!-- Origin -->
        <div class="station-info origin">
          <div class="station-name">{{ trip.transport.origin.name }}</div>
          <div class="timing-info">
            <div class="departure-time" [class.delayed]="trip.transport.origin.isDepartureDelayed">
              <mat-icon>schedule</mat-icon>
              <span>{{ trawellingService.formatTime(trip.transport.origin.departureScheduled) }}</span>
              <span *ngIf="trip.transport.origin.departureReal && 
                          trip.transport.origin.departureReal !== trip.transport.origin.departureScheduled"
                    class="actual-time">
                ({{ trawellingService.formatTime(trip.transport.origin.departureReal) }})
              </span>
            </div>
          </div>
        </div>

        <!-- Arrow -->
        <div class="route-arrow">
          <mat-icon>arrow_forward</mat-icon>
        </div>

        <!-- Destination -->
        <div class="station-info destination">
          <div class="station-name">{{ trip.transport.destination.name }}</div>
          <div class="timing-info">
            <div class="arrival-time" [class.delayed]="trip.transport.destination.isArrivalDelayed">
              <mat-icon>schedule</mat-icon>
              <span>{{ trawellingService.formatTime(trip.transport.destination.arrivalScheduled) }}</span>
              <span *ngIf="trip.transport.destination.arrivalReal && 
                          trip.transport.destination.arrivalReal !== trip.transport.destination.arrivalScheduled"
                    class="actual-time">
                ({{ trawellingService.formatTime(trip.transport.destination.arrivalReal) }})
              </span>
            </div>
          </div>
        </div>
      </mat-card-content>

      <!-- Trip Metadata -->
      <div class="trip-metadata">
        <div class="metadata-item">
          <mat-icon>access_time</mat-icon>
          <span>{{ trawellingService.formatDuration(trip.transport.duration) }}</span>
        </div>
        <div class="metadata-item">
          <mat-icon>straighten</mat-icon>
          <span>{{ trawellingService.formatDistance(trip.transport.distance) }}</span>
        </div>
      </div>

      <!-- Trip Description -->
      <div *ngIf="trip.body" class="trip-description">
        <p>{{ trip.body }}</p>
      </div>

      <!-- Tags -->
      <div *ngIf="trip.tags.length > 0" class="trip-tags">
        <mat-chip-listbox>
          <mat-chip-option *ngFor="let tag of trip.tags" disabled>
            <strong>{{ tag.key }}:</strong> {{ tag.value }}
          </mat-chip-option>
        </mat-chip-listbox>
      </div>

      <!-- Actions -->
      <mat-card-actions class="trip-actions">
        <div class="actions-grid">
          <!-- Connect to Existing RouteInstance -->
          <button 
            mat-raised-button 
            color="primary" 
            (click)="openRouteInstanceSearch()"
            [disabled]="isProcessing">
            <mat-icon>link</mat-icon>
            Connect to RouteInstance
          </button>

          <!-- Connect to Existing Route -->
          <button 
            mat-raised-button 
            color="accent" 
            (click)="openRouteSearch()"
            [disabled]="isProcessing">
            <mat-icon>add_road</mat-icon>
            Add to Existing Route
          </button>

          <!-- Create New Route (Wizard) -->
          <button 
            mat-raised-button 
            (click)="createRouteViaWizard()"
            [disabled]="isProcessing">
            <mat-icon>assistant</mat-icon>
            Create Route (Wizard)
          </button>

          <!-- Create New Route (GPX Upload) -->
          <button 
            mat-raised-button 
            (click)="createRouteViaGPX()"
            [disabled]="isProcessing">
            <mat-icon>upload_file</mat-icon>
            Create Route (GPX)
          </button>

          <!-- Ignore Trip -->
          <button 
            mat-button 
            color="warn" 
            (click)="ignoreTrip()"
            [disabled]="isProcessing">
            <mat-icon>visibility_off</mat-icon>
            Ignore
          </button>
        </div>

        <!-- Processing Indicator -->
        <div *ngIf="isProcessing" class="processing-indicator">
          <mat-spinner diameter="20"></mat-spinner>
          <span>{{ processingMessage }}</span>
        </div>
      </mat-card-actions>
    </mat-card>
  `,
  styleUrls: ['./trip-card.component.scss']
})
export class TripCardComponent {
  @Input() trip!: TrawellingTrip;
  @Output() ignored = new EventEmitter<number>();
  @Output() linked = new EventEmitter<number>();
  @Output() routeCreated = new EventEmitter<number>();

  isProcessing = false;
  processingMessage = '';

  constructor(
    public trawellingService: TrawellingService,
    private router: Router,
    private dialog: MatDialog
  ) {}

  getTransportIcon(): string {
    return this.trawellingService.getTransportIcon(this.trip.transport.category);
  }

  getTransportColor(): string {
    return this.trawellingService.getTransportColor(this.trip.transport.category);
  }

  getJourneyNumber(): string {
    return this.trip.transport.manualJourneyNumber || 
           this.trip.transport.journeyNumber?.toString() || '';
  }

  async ignoreTrip() {
    this.isProcessing = true;
    this.processingMessage = 'Ignoring trip...';
    
    try {
      await this.trawellingService.ignoreTrip(this.trip.id);
      this.ignored.emit(this.trip.id);
    } catch (error) {
      console.error('Error ignoring trip:', error);
    } finally {
      this.isProcessing = false;
    }
  }

  openRouteInstanceSearch() {
    const dialogRef = this.dialog.open(RouteInstanceSearchDialogComponent, {
      width: '90vw',
      maxWidth: '800px',
      data: { trip: this.trip }
    });

    dialogRef.afterClosed().subscribe(result => {
      if (result && result.success) {
        this.linked.emit(this.trip.id);
      }
    });
  }

  openRouteSearch() {
    const dialogRef = this.dialog.open(RouteSearchDialogComponent, {
      width: '90vw',
      maxWidth: '800px',
      data: { trip: this.trip }
    });

    dialogRef.afterClosed().subscribe(result => {
      if (result && result.routeCreated) {
        this.routeCreated.emit(this.trip.id);
      }
    });
  }

  createRouteViaWizard() {
    const tripContext = this.trawellingService.getTripContextForRouteCreation(this.trip);
    // Store context in session storage for the wizard to pick up
    sessionStorage.setItem('trawellingTripContext', JSON.stringify(tripContext));
    this.router.navigate(['/admin/wizard']);
  }

  createRouteViaGPX() {
    const tripContext = this.trawellingService.getTripContextForRouteCreation(this.trip);
    // Store context in session storage for the GPX upload to pick up
    sessionStorage.setItem('trawellingTripContext', JSON.stringify(tripContext));
    this.router.navigate(['/admin/addRoute']);
  }
}