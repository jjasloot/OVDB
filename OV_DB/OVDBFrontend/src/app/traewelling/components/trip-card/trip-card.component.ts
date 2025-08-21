import { Component, Input, Output, EventEmitter } from '@angular/core';
import { CommonModule } from '@angular/common';
import { MatCardModule } from '@angular/material/card';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';
import { MatChipsModule } from '@angular/material/chips';
import { MatMenuModule } from '@angular/material/menu';
import { MatProgressSpinnerModule } from '@angular/material/progress-spinner';
import { MatDividerModule } from '@angular/material/divider';
import { TranslateModule } from '@ngx-translate/core';
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
    MatProgressSpinnerModule,
    MatDividerModule,
    TranslateModule
  ],
  template: `
    <mat-card class="trip-card">
      <!-- Compact Trip Header -->
      <div class="trip-header">
        <!-- Transport Icon and Line Info -->
        <div class="transport-info">
          <div class="transport-icon" [style.background-color]="getTransportColor()">
            <mat-icon>{{ getTransportIcon() }}</mat-icon>
          </div>
          <div class="line-info">
            <div class="line-number">{{ trip.transport.lineName }}</div>
            <div *ngIf="getJourneyNumber()" class="journey-number">{{ getJourneyNumber() }}</div>
          </div>
        </div>

        <!-- Date and Duration -->
        <div class="trip-meta">
          <div class="trip-date">
            <mat-icon>event</mat-icon>
            {{ trawellingService.formatDate(trip.createdAt) }}
          </div>
          <div class="trip-duration">
            <mat-icon>schedule</mat-icon>
            {{ trawellingService.formatDuration(trip.transport.duration) }}
          </div>
        </div>
      </div>

      <mat-divider></mat-divider>

      <!-- Route Information (Compact) -->
      <div class="route-info">
        <div class="station-row origin">
          <div class="station-content">
            <span class="station-name">{{ trip.transport.origin.name }}</span>
            <div class="timing" [class.delayed]="trip.transport.origin.isDepartureDelayed">
              <span class="scheduled-time">{{ trawellingService.formatTime(trip.transport.origin.departureScheduled) }}</span>
              <span *ngIf="trip.transport.origin.departureReal && 
                          trip.transport.origin.departureReal !== trip.transport.origin.departureScheduled"
                    class="actual-time">
                ({{ trawellingService.formatTime(trip.transport.origin.departureReal) }})
              </span>
            </div>
          </div>
        </div>

        <div class="route-connector">
          <mat-icon>arrow_downward</mat-icon>
          <div class="distance">{{ trawellingService.formatDistance(trip.transport.distance) }}</div>
        </div>

        <div class="station-row destination">
          <div class="station-content">
            <span class="station-name">{{ trip.transport.destination.name }}</span>
            <div class="timing" [class.delayed]="trip.transport.destination.isArrivalDelayed">
              <span class="scheduled-time">{{ trawellingService.formatTime(trip.transport.destination.arrivalScheduled) }}</span>
              <span *ngIf="trip.transport.destination.arrivalReal && 
                          trip.transport.destination.arrivalReal !== trip.transport.destination.arrivalScheduled"
                    class="actual-time">
                ({{ trawellingService.formatTime(trip.transport.destination.arrivalReal) }})
              </span>
            </div>
          </div>
        </div>
      </div>

      <!-- Trip Description (Compact) -->
      <div *ngIf="trip.body" class="trip-description">
        <p>{{ trip.body }}</p>
      </div>

      <!-- Tags (Improved) -->
      <div *ngIf="trip.tags.length > 0" class="trip-tags">
        <mat-chip-listbox class="tag-chips">
          <mat-chip-option *ngFor="let tag of trip.tags" disabled class="tag-chip">
            <span class="tag-key">{{ tag.key }}</span>
            <span class="tag-value">{{ tag.value }}</span>
          </mat-chip-option>
        </mat-chip-listbox>
      </div>

      <mat-divider></mat-divider>

      <!-- Compact Actions -->
      <mat-card-actions class="trip-actions">
        <div class="actions-row">
          <!-- Primary Actions -->
          <div class="primary-actions">
            <button 
              mat-mini-fab 
              color="primary" 
              (click)="openRouteInstanceSearch()"
              [disabled]="isProcessing"
              [title]="'TRAEWELLING.CONNECT_TO_INSTANCE' | translate">
              <mat-icon>link</mat-icon>
            </button>

            <button 
              mat-mini-fab 
              color="accent" 
              (click)="openRouteSearch()"
              [disabled]="isProcessing"
              [title]="'TRAEWELLING.ADD_TO_ROUTE' | translate">
              <mat-icon>add_road</mat-icon>
            </button>

            <button 
              mat-mini-fab 
              (click)="createRouteViaWizard()"
              [disabled]="isProcessing"
              [title]="'TRAEWELLING.CREATE_WIZARD' | translate">
              <mat-icon>assistant</mat-icon>
            </button>

            <button 
              mat-mini-fab 
              (click)="createRouteViaGPX()"
              [disabled]="isProcessing"
              [title]="'TRAEWELLING.CREATE_GPX' | translate">
              <mat-icon>upload_file</mat-icon>
            </button>
          </div>

          <!-- Secondary Actions -->
          <div class="secondary-actions">
            <button 
              mat-icon-button 
              color="warn" 
              (click)="ignoreTrip()"
              [disabled]="isProcessing"
              [title]="'TRAEWELLING.IGNORE_TRIP' | translate">
              <mat-icon>visibility_off</mat-icon>
            </button>
          </div>
        </div>

        <!-- Processing Indicator -->
        <div *ngIf="isProcessing" class="processing-indicator">
          <mat-spinner diameter="16"></mat-spinner>
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