import { Component, Input, Output, EventEmitter } from '@angular/core';
import { CommonModule } from '@angular/common';
import { Router } from '@angular/router';
import { MatCardModule } from '@angular/material/card';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';
import { MatChipsModule } from '@angular/material/chips';
import { MatProgressSpinnerModule } from '@angular/material/progress-spinner';
import { MatDialog } from '@angular/material/dialog';
import { TranslateModule } from '@ngx-translate/core';
import { TrawellingTrip, TrawellingHafasTravelType } from '../../../models/traewelling.model';
import { TrawellingService } from '../../services/traewelling.service';
import { RouteSearchDialogComponent } from '../../dialogs/route-search-dialog/route-search-dialog.component';
import { RouteInstanceSearchDialogComponent } from '../../dialogs/route-instance-search-dialog/route-instance-search-dialog.component';

@Component({
  selector: 'app-trip-card',
  standalone: true,
  imports: [
    CommonModule,
    MatCardModule,
    MatButtonModule,
    MatIconModule,
    MatChipsModule,
    MatProgressSpinnerModule,
    TranslateModule
  ],
  template: `
    <mat-card class="trip-card">
      <!-- Trip Header -->
      <mat-card-header>
        <mat-icon 
          mat-card-avatar 
          class="transport-icon"
          [class]="'transport-' + getTransportClass(trip.transport.category)">
          {{ getTransportIcon(trip.transport.category) }}
        </mat-icon>
        <mat-card-title>
          <div class="trip-title">
            {{ trip.transport.lineName }}
            @if (trip.transport.journeyNumber || trip.transport.number) {
              <span class="journey-number">
                {{ trip.transport.journeyNumber || trip.transport.number }}
              </span>
            }
          </div>
        </mat-card-title>
        <mat-card-subtitle>
          {{ trip.transport.origin.name }} → {{ trip.transport.destination.name }}
        </mat-card-subtitle>
      </mat-card-header>

      <!-- Trip Details -->
      <mat-card-content>
        <!-- Basic Info Chips -->
        <div class="basic-info">
          @if (trip.createdAt) {
            <mat-chip>
              <mat-icon>event</mat-icon>
              {{ trip.createdAt | date:'mediumDate' }}
            </mat-chip>
          }
          @if (trip.transport.duration) {
            <mat-chip>
              <mat-icon>timer</mat-icon>
              {{ formatDuration(trip.transport.duration) }}
            </mat-chip>
          }
          @if (trip.transport.distance) {
            <mat-chip>
              <mat-icon>straighten</mat-icon>
              {{ formatDistance(trip.transport.distance) }}
            </mat-chip>
          }
        </div>

        <!-- Timing Grid -->
        <div class="timing-grid">
          <!-- Departure Column -->
          <div class="timing-column">
            <h4>{{ 'TRAEWELLING.DEPARTURE' | translate }}</h4>
            <div class="time-info">
              @if (trip.transport.origin.departureScheduled) {
                <div class="time-row">
                  <mat-chip class="time-chip scheduled">
                    <mat-icon>schedule</mat-icon>
                    {{ trip.transport.origin.departureScheduled | date:'shortTime' }}
                  </mat-chip>
                  @if (trip.transport.origin.departurePlatformPlanned) {
                    <mat-chip class="platform-chip">
                      {{ 'TRAEWELLING.PLATFORM' | translate }} {{ trip.transport.origin.departurePlatformPlanned }}
                    </mat-chip>
                  }
                </div>
              }
              @if (trip.transport.origin.departureReal) {
                <div class="time-row">
                  <mat-chip 
                    class="time-chip actual"
                    [class.delayed]="isDelayed(trip.transport.origin.departureScheduled, trip.transport.origin.departureReal)">
                    <mat-icon>
                      {{ isDelayed(trip.transport.origin.departureScheduled, trip.transport.origin.departureReal) ? 'warning' : 'check' }}
                    </mat-icon>
                    {{ trip.transport.origin.departureReal | date:'shortTime' }}
                  </mat-chip>
                  @if (trip.transport.origin.departurePlatformReal) {
                    <mat-chip class="platform-chip">
                      {{ 'TRAEWELLING.PLATFORM' | translate }} {{ trip.transport.origin.departurePlatformReal }}
                    </mat-chip>
                  }
                </div>
              }
            </div>
          </div>

          <!-- Arrival Column -->
          @if (trip.transport.destination.arrivalScheduled || trip.transport.destination.arrivalReal) {
            <div class="timing-column">
              <h4>{{ 'TRAEWELLING.ARRIVAL' | translate }}</h4>
              <div class="time-info">
                @if (trip.transport.destination.arrivalScheduled) {
                  <div class="time-row">
                    <mat-chip class="time-chip scheduled">
                      <mat-icon>schedule</mat-icon>
                      {{ trip.transport.destination.arrivalScheduled | date:'shortTime' }}
                    </mat-chip>
                    @if (trip.transport.destination.arrivalPlatformPlanned) {
                      <mat-chip class="platform-chip">
                        {{ 'TRAEWELLING.PLATFORM' | translate }} {{ trip.transport.destination.arrivalPlatformPlanned }}
                      </mat-chip>
                    }
                  </div>
                }
                @if (trip.transport.destination.arrivalReal) {
                  <div class="time-row">
                    <mat-chip 
                      class="time-chip actual"
                      [class.delayed]="isDelayed(trip.transport.destination.arrivalScheduled, trip.transport.destination.arrivalReal)">
                      <mat-icon>
                        {{ isDelayed(trip.transport.destination.arrivalScheduled, trip.transport.destination.arrivalReal) ? 'warning' : 'check' }}
                      </mat-icon>
                      {{ trip.transport.destination.arrivalReal | date:'shortTime' }}
                    </mat-chip>
                    @if (trip.transport.destination.arrivalPlatformReal) {
                      <mat-chip class="platform-chip">
                        {{ 'TRAEWELLING.PLATFORM' | translate }} {{ trip.transport.destination.arrivalPlatformReal }}
                      </mat-chip>
                    }
                  </div>
                }
              </div>
            </div>
          }
        </div>

        <!-- Description -->
        @if (trip.body) {
          <div class="trip-description">
            <mat-icon>description</mat-icon>
            <span>{{ trip.body }}</span>
          </div>
        }

        <!-- Tags -->
        @if (trip.tags && trip.tags.length > 0) {
          <div class="trip-tags">
            <h4>{{ 'TRAEWELLING.TAGS' | translate }}</h4>
            <div class="tags-container">
              @for (tag of trip.tags; track tag.key) {
                <mat-chip class="tag-chip">
                  <strong>{{ tag.key }}:</strong> {{ tag.value }}
                </mat-chip>
              }
            </div>
          </div>
        }
      </mat-card-content>

      <!-- Actions -->
      <mat-card-actions class="trip-actions">
        <button 
          mat-raised-button 
          color="primary" 
          (click)="searchRouteInstances()"
          [disabled]="processing">
          <mat-icon>search</mat-icon>
          {{ 'TRAEWELLING.SEARCH_ROUTE_INSTANCES' | translate }}
        </button>

        <button 
          mat-raised-button 
          color="accent" 
          (click)="addToExistingRoute()"
          [disabled]="processing">
          <mat-icon>add_location_alt</mat-icon>
          {{ 'TRAEWELLING.ADD_TO_EXISTING_ROUTE' | translate }}
        </button>

        <button 
          mat-raised-button 
          (click)="createNewRouteWizard()"
          [disabled]="processing">
          <mat-icon>auto_fix_high</mat-icon>
          {{ 'TRAEWELLING.USE_WIZARD' | translate }}
        </button>

        <button 
          mat-raised-button 
          (click)="createNewRouteGpx()"
          [disabled]="processing">
          <mat-icon>upload</mat-icon>
          {{ 'TRAEWELLING.UPLOAD_GPX' | translate }}
        </button>

        <button 
          mat-button 
          color="warn" 
          (click)="ignoreTrip()"
          [disabled]="processing">
          <mat-icon>visibility_off</mat-icon>
          {{ 'TRAEWELLING.IGNORE_TRIP' | translate }}
        </button>
      </mat-card-actions>

      @if (processing) {
        <div class="processing-overlay">
          <mat-spinner diameter="32"></mat-spinner>
        </div>
      }
    </mat-card>
  `,
  styles: [`
    .trip-card {
      position: relative;
      max-width: 100%;
      margin-bottom: 16px;
    }

    .transport-icon {
      width: 40px;
      height: 40px;
      font-size: 24px;
      display: flex;
      align-items: center;
      justify-content: center;
      border-radius: 50%;
    }

    .transport-icon.transport-train {
      background-color: rgba(33, 150, 243, 0.1);
      color: #2196f3;
    }

    .transport-icon.transport-bus {
      background-color: rgba(255, 152, 0, 0.1);
      color: #ff9800;
    }

    .transport-icon.transport-tram {
      background-color: rgba(156, 39, 176, 0.1);
      color: #9c27b0;
    }

    .transport-icon.transport-subway {
      background-color: rgba(76, 175, 80, 0.1);
      color: #4caf50;
    }

    .transport-icon.transport-ferry {
      background-color: rgba(3, 169, 244, 0.1);
      color: #03a9f4;
    }

    .transport-icon.transport-taxi {
      background-color: rgba(255, 193, 7, 0.1);
      color: #ffc107;
    }

    .trip-title {
      display: flex;
      align-items: center;
      gap: 8px;
      font-weight: 500;
    }

    .journey-number {
      font-size: 14px;
      opacity: 0.7;
      font-weight: normal;
    }

    .basic-info {
      display: flex;
      flex-wrap: wrap;
      gap: 8px;
      margin-bottom: 16px;
    }

    .basic-info mat-chip {
      display: flex;
      align-items: center;
      gap: 4px;
    }

    .timing-grid {
      display: grid;
      grid-template-columns: 1fr 1fr;
      gap: 16px;
      margin-bottom: 16px;
    }

    .timing-column h4 {
      font-size: 12px;
      font-weight: 500;
      text-transform: uppercase;
      margin: 0 0 8px 0;
      opacity: 0.7;
      letter-spacing: 0.5px;
    }

    .time-info {
      display: flex;
      flex-direction: column;
      gap: 4px;
    }

    .time-row {
      display: flex;
      flex-wrap: wrap;
      gap: 4px;
      align-items: center;
    }

    .time-chip {
      display: flex;
      align-items: center;
      gap: 4px;
      font-weight: 500;
    }

    .time-chip.scheduled {
      background-color: rgba(158, 158, 158, 0.2);
    }

    .time-chip.actual {
      background-color: rgba(76, 175, 80, 0.2);
    }

    .time-chip.delayed {
      background-color: rgba(244, 67, 54, 0.2);
      color: #f44336;
    }

    .platform-chip {
      font-size: 11px;
      min-height: 24px;
      opacity: 0.8;
    }

    .trip-description {
      display: flex;
      align-items: flex-start;
      gap: 8px;
      margin-bottom: 16px;
      padding: 12px;
      border-radius: 8px;
      background-color: rgba(33, 150, 243, 0.08);
      border-left: 3px solid #2196f3;
    }

    .trip-description mat-icon {
      color: #2196f3;
      margin-top: 2px;
      flex-shrink: 0;
    }

    .trip-tags h4 {
      font-size: 12px;
      font-weight: 500;
      text-transform: uppercase;
      margin: 0 0 8px 0;
      opacity: 0.7;
      letter-spacing: 0.5px;
    }

    .tags-container {
      display: flex;
      flex-wrap: wrap;
      gap: 8px;
    }

    .tag-chip {
      background-color: rgba(63, 81, 181, 0.1);
      border: 1px solid rgba(63, 81, 181, 0.2);
    }

    .trip-actions {
      display: grid;
      grid-template-columns: repeat(auto-fit, minmax(160px, 1fr));
      gap: 8px;
      padding: 16px;
    }

    .trip-actions button {
      display: flex;
      align-items: center;
      gap: 8px;
      justify-content: center;
    }

    .processing-overlay {
      position: absolute;
      top: 0;
      left: 0;
      right: 0;
      bottom: 0;
      background-color: rgba(255, 255, 255, 0.8);
      display: flex;
      align-items: center;
      justify-content: center;
      z-index: 10;
    }

    /* Dark mode support */
    @media (prefers-color-scheme: dark) {
      .processing-overlay {
        background-color: rgba(0, 0, 0, 0.8);
      }
    }

    /* Mobile responsiveness */
    @media (max-width: 768px) {
      .timing-grid {
        grid-template-columns: 1fr;
        gap: 12px;
      }

      .trip-actions {
        grid-template-columns: 1fr;
      }

      .basic-info {
        justify-content: center;
      }

      .time-row {
        justify-content: flex-start;
      }
    }

    @media (max-width: 480px) {
      .trip-title {
        flex-direction: column;
        align-items: flex-start;
        gap: 4px;
      }

      .time-row {
        flex-direction: column;
        align-items: flex-start;
      }
    }
  `]
})
export class TripCardComponent {
  @Input() trip!: TrawellingTrip;
  @Output() actionPerformed = new EventEmitter<{action: string, trip: TrawellingTrip, data?: any}>();

  processing = false;

  constructor(
    private router: Router,
    private dialog: MatDialog,
    private trawellingService: TrawellingService
  ) {}

  getTransportIcon(category: TrawellingHafasTravelType): string {
    const iconMap = {
      [TrawellingHafasTravelType.BUS]: 'directions_bus',
      [TrawellingHafasTravelType.NATIONAL]: 'train',
      [TrawellingHafasTravelType.NATIONAL_EXPRESS]: 'train',
      [TrawellingHafasTravelType.REGIONAL]: 'train',
      [TrawellingHafasTravelType.REGIONAL_EXP]: 'train',
      [TrawellingHafasTravelType.SUBWAY]: 'subway',
      [TrawellingHafasTravelType.TRAM]: 'tram',
      [TrawellingHafasTravelType.FERRY]: 'directions_boat',
      [TrawellingHafasTravelType.TAXI]: 'local_taxi'
    };
    return iconMap[category] || 'train';
  }

  getTransportClass(category: TrawellingHafasTravelType): string {
    const classMap = {
      [TrawellingHafasTravelType.BUS]: 'bus',
      [TrawellingHafasTravelType.NATIONAL]: 'train',
      [TrawellingHafasTravelType.NATIONAL_EXPRESS]: 'train',
      [TrawellingHafasTravelType.REGIONAL]: 'train',
      [TrawellingHafasTravelType.REGIONAL_EXP]: 'train',
      [TrawellingHafasTravelType.SUBWAY]: 'subway',
      [TrawellingHafasTravelType.TRAM]: 'tram',
      [TrawellingHafasTravelType.FERRY]: 'ferry',
      [TrawellingHafasTravelType.TAXI]: 'taxi'
    };
    return classMap[category] || 'train';
  }

  formatDuration(minutes: number): string {
    const hours = Math.floor(minutes / 60);
    const mins = minutes % 60;
    return hours > 0 ? `${hours}h ${mins}m` : `${mins}m`;
  }

  formatDistance(meters: number): string {
    return meters >= 1000 ? `${(meters / 1000).toFixed(1)} km` : `${meters} m`;
  }

  isDelayed(planned: string | undefined, actual: string | undefined): boolean {
    if (!planned || !actual) return false;
    return new Date(actual) > new Date(planned);
  }

  async searchRouteInstances() {
    const dialogRef = this.dialog.open(RouteInstanceSearchDialogComponent, {
      width: '90vw',
      maxWidth: '800px',
      data: { trip: this.trip }
    });

    const result = await dialogRef.afterClosed().toPromise();
    if (result?.success) {
      this.actionPerformed.emit({ action: 'linked', trip: this.trip, data: result });
    }
  }

  async addToExistingRoute() {
    const dialogRef = this.dialog.open(RouteSearchDialogComponent, {
      width: '90vw',
      maxWidth: '600px',
      data: { trip: this.trip, action: 'addToRoute' }
    });

    const result = await dialogRef.afterClosed().toPromise();
    if (result?.success) {
      this.actionPerformed.emit({ action: 'addedToRoute', trip: this.trip, data: result });
    }
  }

  createNewRouteWizard() {
    const tripData = this.trawellingService.getTripDataForRouteCreation(this.trip);
    this.router.navigate(['/admin/wizard'], { 
      queryParams: { 
        trawellingTripId: this.trip.id,
        ...tripData 
      } 
    });
  }

  createNewRouteGpx() {
    const tripData = this.trawellingService.getTripDataForRouteCreation(this.trip);
    this.router.navigate(['/admin/addRoute'], { 
      queryParams: { 
        trawellingTripId: this.trip.id,
        ...tripData 
      } 
    });
  }

  async ignoreTrip() {
    this.processing = true;
    try {
      const result = await this.trawellingService.ignoreTrip({ statusId: this.trip.id });
      if (result.success) {
        this.actionPerformed.emit({ action: 'ignored', trip: this.trip });
      }
    } catch (error) {
      console.error('Error ignoring trip:', error);
    } finally {
      this.processing = false;
    }
  }
}