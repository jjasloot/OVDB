import { Component, Input, Output, EventEmitter } from '@angular/core';
import { CommonModule } from '@angular/common';
import { MatButtonModule } from '@angular/material/button';
import { MatCardModule } from '@angular/material/card';
import { MatChipsModule } from '@angular/material/chips';
import { MatIconModule } from '@angular/material/icon';
import { MatProgressSpinnerModule } from '@angular/material/progress-spinner';
import { TranslateModule } from '@ngx-translate/core';
import { TrawellingTrip, TrawellingHafasTravelType } from '../../models/traewelling.model';

@Component({
  selector: 'app-trip-card',
  standalone: true,
  imports: [
    CommonModule,
    MatButtonModule,
    MatCardModule,
    MatChipsModule,
    MatIconModule,
    MatProgressSpinnerModule,
    TranslateModule
  ],
  template: `
    <mat-card class="trip-card">
      <!-- Trip Header -->
      <mat-card-header>
        <mat-card-title>
          <div class="trip-header">
            @if (trip.transport?.category) {
              <mat-icon class="transport-icon">{{ getCategoryIcon(trip.transport.category) }}</mat-icon>
            }
            <span class="transport-line">
              {{ trip.transport?.lineName }}
              @if(trip.transport?.lineName !== trip.transport?.number) {
                {{ trip.transport?.number }}
              }
            </span>
          </div>
        </mat-card-title>
        <mat-card-subtitle>
          {{ trip.transport?.origin?.name }} → {{ trip.transport?.destination?.name }}
        </mat-card-subtitle>
      </mat-card-header>

      <!-- Trip Content -->
      <mat-card-content>
        <!-- Trip Details Chips -->
        <div class="trip-details">
          @if (trip.createdAt) {
            <mat-chip>
              <mat-icon>event</mat-icon>
              {{ trip.createdAt | date:'mediumDate' }}
            </mat-chip>
          }
          @if (trip.transport?.duration) {
            <mat-chip>
              <mat-icon>timer</mat-icon>
              {{ formatDuration(trip.transport.duration) }}
            </mat-chip>
          }
          @if (trip.transport?.distance) {
            <mat-chip>
              <mat-icon>straighten</mat-icon>
              {{ formatDistance(trip.transport.distance) }}
            </mat-chip>
          }
        </div>

        <!-- Timing Information -->
        <div class="timing-section">
          <div class="timing-row">
            <!-- Departure -->
            <div class="timing-column">
              <h4>{{ 'TRAEWELLING.DEPARTURE' | translate }}</h4>
              <div class="time-chips">
                @if (trip.transport?.origin?.departureScheduled) {
                  <mat-chip>
                    <mat-icon>schedule</mat-icon>
                    {{ trip.transport.origin.departureScheduled | date:'shortTime' }}
                  </mat-chip>
                }
                @if (trip.transport?.origin?.departureReal) {
                  <mat-chip [color]="isDelayed(trip.transport.origin.departureScheduled, trip.transport.origin.departureReal) ? 'warn' : 'primary'">
                    <mat-icon>{{ isDelayed(trip.transport.origin.departureScheduled, trip.transport.origin.departureReal) ? 'warning' : 'check' }}</mat-icon>
                    {{ trip.transport.origin.departureReal | date:'shortTime' }}
                  </mat-chip>
                }
                @if (trip.transport?.origin?.departurePlatformPlanned) {
                  <mat-chip>
                    <mat-icon>train</mat-icon>
                    {{ 'TRAEWELLING.PLATFORM' | translate }} {{ trip.transport.origin.departurePlatformPlanned }}
                  </mat-chip>
                }
              </div>
            </div>

            <!-- Arrival -->
            @if (trip.transport?.destination?.arrivalScheduled || trip.transport?.destination?.arrivalReal) {
              <div class="timing-column">
                <h4>{{ 'TRAEWELLING.ARRIVAL' | translate }}</h4>
                <div class="time-chips">
                  @if (trip.transport?.destination?.arrivalScheduled) {
                    <mat-chip>
                      <mat-icon>schedule</mat-icon>
                      {{ trip.transport.destination.arrivalScheduled | date:'shortTime' }}
                    </mat-chip>
                  }
                  @if (trip.transport?.destination?.arrivalReal) {
                    <mat-chip [color]="isDelayed(trip.transport.destination.arrivalScheduled, trip.transport.destination.arrivalReal) ? 'warn' : 'primary'">
                      <mat-icon>{{ isDelayed(trip.transport.destination.arrivalScheduled, trip.transport.destination.arrivalReal) ? 'warning' : 'check' }}</mat-icon>
                      {{ trip.transport.destination.arrivalReal | date:'shortTime' }}
                    </mat-chip>
                  }
                  @if (trip.transport?.destination?.arrivalPlatformPlanned) {
                    <mat-chip>
                      <mat-icon>train</mat-icon>
                      {{ 'TRAEWELLING.PLATFORM' | translate }} {{ trip.transport.destination.arrivalPlatformPlanned }}
                    </mat-chip>
                  }
                </div>
              </div>
            }
          </div>
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
                  <span class="tag-key">{{ tag.key }}:</span>
                  <span class="tag-value">{{ tag.value }}</span>
                </mat-chip>
              }
            </div>
          </div>
        }
      </mat-card-content>

      <!-- Actions -->
      <mat-card-actions class="trip-actions">
        <button mat-raised-button color="accent" (click)="addToExistingRoute.emit(trip)">
          <mat-icon>add_location_alt</mat-icon>
          {{ 'TRAEWELLING.ADD_TO_EXISTING_ROUTE' | translate }}
        </button>

        <button mat-raised-button (click)="createNewRoute.emit(trip)">
          <mat-icon>create</mat-icon>
          {{ 'TRAEWELLING.CREATE_NEW_ROUTE' | translate }}
        </button>

        <button mat-raised-button color="primary" (click)="searchRouteInstances.emit(trip)">
          <mat-icon>search</mat-icon>
          {{ 'TRAEWELLING.SEARCH_ROUTE_INSTANCES' | translate }}
        </button>

        <button mat-raised-button color="warn" (click)="ignoreTrip.emit(trip)">
          <mat-icon>visibility_off</mat-icon>
          {{ 'TRAEWELLING.IGNORE_TRIP' | translate }}
        </button>
      </mat-card-actions>

      <!-- Route Instance Search Results -->
      @if (showRouteInstances) {
        <mat-card-content class="route-search-results">
          @if (searchingRouteInstances) {
            <div class="loading-section">
              <mat-progress-spinner diameter="20"></mat-progress-spinner>
              <span>{{ 'TRAEWELLING.SEARCHING_ROUTE_INSTANCES' | translate }}</span>
            </div>
          } @else if (routeInstances.length > 0) {
            <h4>{{ 'TRAEWELLING.SELECT_ROUTE_INSTANCE' | translate }}</h4>
            <div class="route-instances-list">
              @for (routeInstance of routeInstances; track routeInstance.id) {
                <mat-card class="route-instance-card" [class.disabled]="routeInstance.hasTraewellingLink">
                  <mat-card-content>
                    <div class="route-instance-info">
                      <h5>{{ routeInstance.routeName || (routeInstance.from + ' - ' + routeInstance.to) }}</h5>
                      <p>{{ routeInstance.from }} → {{ routeInstance.to }}</p>
                      <div class="route-instance-details">
                        @if (routeInstance.startTime) {
                          <mat-chip>
                            <mat-icon>schedule</mat-icon>
                            {{ routeInstance.startTime | date:'short' }}
                          </mat-chip>
                        }
                        @if (routeInstance.hasTraewellingLink) {
                          <mat-chip color="accent">
                            <mat-icon>link</mat-icon>
                            {{ 'TRAEWELLING.ALREADY_LINKED' | translate }}
                          </mat-chip>
                        }
                      </div>
                    </div>
                  </mat-card-content>
                  <mat-card-actions>
                    <button 
                      mat-button 
                      color="primary" 
                      [disabled]="routeInstance.hasTraewellingLink"
                      (click)="linkToRouteInstance.emit({trip: trip, routeInstance: routeInstance})">
                      <mat-icon>link</mat-icon>
                      {{ routeInstance.hasTraewellingLink ? ('TRAEWELLING.ALREADY_LINKED' | translate) : ('TRAEWELLING.LINK_TO_EXISTING' | translate) }}
                    </button>
                  </mat-card-actions>
                </mat-card>
              }
            </div>
          } @else {
            <div class="no-results">
              <mat-icon>no_transfer</mat-icon>
              <span>{{ 'TRAEWELLING.NO_ROUTE_INSTANCES_FOUND' | translate }}</span>
            </div>
          }
        </mat-card-content>
      }
    </mat-card>
  `,
  styles: [`
    .trip-card {
      margin-bottom: 24px;
    }

    .trip-header {
      display: flex;
      align-items: center;
      gap: 8px;
    }

    .transport-icon {
      color: #1976d2;
    }

    .transport-line {
      font-weight: 500;
    }

    .trip-details {
      display: flex;
      flex-wrap: wrap;
      gap: 8px;
      margin-bottom: 16px;
    }

    .trip-details mat-chip {
      display: flex;
      align-items: center;
      gap: 4px;
    }

    .timing-section {
      margin-bottom: 16px;
    }

    .timing-row {
      display: grid;
      grid-template-columns: 1fr 1fr;
      gap: 16px;
    }

    .timing-column h4 {
      font-size: 14px;
      font-weight: 500;
      margin: 0 0 8px 0;
      opacity: 0.7;
      text-transform: uppercase;
    }

    .time-chips {
      display: flex;
      flex-wrap: wrap;
      gap: 4px;
    }

    .time-chips mat-chip {
      display: flex;
      align-items: center;
      gap: 4px;
    }

    .trip-description {
      display: flex;
      align-items: flex-start;
      gap: 8px;
      margin-bottom: 16px;
      padding: 12px;
      border-radius: 8px;
      background-color: rgba(255, 152, 0, 0.1);
      border-left: 3px solid #ff9800;
    }

    .trip-description mat-icon {
      color: #ff9800;
      margin-top: 2px;
      flex-shrink: 0;
    }

    .trip-tags {
      margin-bottom: 16px;
    }

    .trip-tags h4 {
      font-size: 14px;
      font-weight: 500;
      margin: 0 0 8px 0;
      opacity: 0.7;
    }

    .tags-container {
      display: flex;
      flex-wrap: wrap;
      gap: 8px;
    }

    .tag-chip {
      display: flex;
      align-items: center;
      gap: 4px;
    }

    .tag-key {
      font-weight: 500;
      color: #1976d2;
    }

    .tag-value {
      opacity: 0.8;
    }

    .trip-actions {
      display: flex;
      flex-wrap: wrap;
      gap: 8px;
      padding: 16px;
    }

    .trip-actions button {
      display: flex;
      align-items: center;
      gap: 8px;
    }

    .route-search-results {
      border-top: 1px solid rgba(0, 0, 0, 0.12);
      padding-top: 16px;
    }

    .loading-section {
      display: flex;
      align-items: center;
      gap: 12px;
      padding: 16px;
    }

    .route-instances-list {
      margin-top: 12px;
    }

    .route-instance-card {
      margin-bottom: 12px;
    }

    .route-instance-card.disabled {
      opacity: 0.7;
    }

    .route-instance-info h5 {
      margin: 0 0 4px 0;
      font-weight: 500;
    }

    .route-instance-info p {
      margin: 0 0 8px 0;
      font-size: 14px;
      opacity: 0.7;
    }

    .route-instance-details {
      display: flex;
      flex-wrap: wrap;
      gap: 4px;
    }

    .route-instance-details mat-chip {
      display: flex;
      align-items: center;
      gap: 4px;
      font-size: 12px;
    }

    .no-results {
      display: flex;
      align-items: center;
      gap: 8px;
      padding: 16px;
      opacity: 0.7;
    }

    /* Mobile responsiveness */
    @media (max-width: 768px) {
      .timing-row {
        grid-template-columns: 1fr;
      }

      .trip-actions {
        flex-direction: column;
      }

      .trip-actions button {
        width: 100%;
        justify-content: center;
      }

      .time-chips {
        flex-direction: column;
        align-items: flex-start;
      }

      .time-chips mat-chip {
        width: 100%;
        justify-content: flex-start;
      }
    }
  `]
})
export class TripCardComponent {
  @Input() trip!: TrawellingTrip;
  @Input() showRouteInstances = false;
  @Input() searchingRouteInstances = false;
  @Input() routeInstances: any[] = [];

  @Output() addToExistingRoute = new EventEmitter<TrawellingTrip>();
  @Output() createNewRoute = new EventEmitter<TrawellingTrip>();
  @Output() searchRouteInstances = new EventEmitter<TrawellingTrip>();
  @Output() ignoreTrip = new EventEmitter<TrawellingTrip>();
  @Output() linkToRouteInstance = new EventEmitter<{trip: TrawellingTrip, routeInstance: any}>();

  getCategoryIcon(category: TrawellingHafasTravelType): string {
    const iconMap = {
      [TrawellingHafasTravelType.NATIONAL_EXPRESS]: 'speed',
      [TrawellingHafasTravelType.NATIONAL]: 'train',
      [TrawellingHafasTravelType.REGIONAL_EXP]: 'directions_transit',
      [TrawellingHafasTravelType.REGIONAL]: 'commute',
      [TrawellingHafasTravelType.SUBURBAN]: 'subway',
      [TrawellingHafasTravelType.SUBWAY]: 'subway',
      [TrawellingHafasTravelType.TRAM]: 'tram',
      [TrawellingHafasTravelType.BUS]: 'directions_bus',
      [TrawellingHafasTravelType.FERRY]: 'directions_boat',
      [TrawellingHafasTravelType.TAXI]: 'local_taxi',
      [TrawellingHafasTravelType.PLANE]: 'flight'
    };
    return iconMap[category] || 'train';
  }

  formatDuration(minutes: number): string {
    const hours = Math.floor(minutes / 60);
    const mins = minutes % 60;
    return hours > 0 ? `${hours}h ${mins}m` : `${mins}m`;
  }

  formatDistance(meters: number): string {
    return meters >= 1000 ? `${(meters / 1000).toFixed(1)} km` : `${meters} m`;
  }

  isDelayed(planned: string | null, actual: string | null): boolean {
    if (!planned || !actual) return false;
    return new Date(actual) > new Date(planned);
  }
}