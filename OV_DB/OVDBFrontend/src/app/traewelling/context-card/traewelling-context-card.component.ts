import { Component, Input } from '@angular/core';
import { CommonModule } from '@angular/common';
import { MatCardModule } from '@angular/material/card';
import { MatChipsModule } from '@angular/material/chips';
import { MatIconModule } from '@angular/material/icon';
import { TranslateModule } from '@ngx-translate/core';
import { TrawellingTrip, TrawellingHafasTravelType } from '../../models/traewelling.model';

@Component({
  selector: 'app-traewelling-context-card',
  standalone: true,
  imports: [
    CommonModule,
    MatCardModule,
    MatChipsModule,
    MatIconModule,
    TranslateModule
  ],
  template: `
    @if (trip) {
      <mat-card class="context-card">
        <mat-card-header>
          <mat-icon mat-card-avatar class="transport-icon">
            {{ getTransportIcon(trip.transport.category) }}
          </mat-icon>
          <mat-card-title>
            {{ trip.transport.lineName }}
            @if (trip.transport.journeyNumber || trip.transport.number) {
              {{ trip.transport.journeyNumber || trip.transport.number }}
            }
          </mat-card-title>
          <mat-card-subtitle>
            {{ trip.transport.origin.name }} → {{ trip.transport.destination.name }}
          </mat-card-subtitle>
        </mat-card-header>

        <mat-card-content>
          <!-- Timing Info -->
          <div class="timing-info">
            @if (trip.transport.origin.departureScheduled) {
              <mat-chip>
                <mat-icon>schedule</mat-icon>
                {{ trip.transport.origin.departureScheduled | date:'short' }}
              </mat-chip>
            }
            @if (trip.transport.destination.arrivalScheduled) {
              <mat-chip>
                <mat-icon>flag</mat-icon>
                {{ trip.transport.destination.arrivalScheduled | date:'short' }}
              </mat-chip>
            }
            @if (trip.transport.duration) {
              <mat-chip>
                <mat-icon>timer</mat-icon>
                {{ formatDuration(trip.transport.duration) }}
              </mat-chip>
            }
          </div>

          <!-- Description -->
          @if (trip.body) {
            <div class="trip-description">
              <strong>{{ 'TRAEWELLING.TRIP_DESCRIPTION' | translate }}:</strong>
              {{ trip.body }}
            </div>
          }

          <!-- Tags -->
          @if (trip.tags && trip.tags.length > 0) {
            <div class="trip-tags">
              <strong>{{ 'TRAEWELLING.TAGS' | translate }}:</strong>
              <div class="tags-container">
                @for (tag of trip.tags; track tag.key) {
                  <mat-chip class="tag-chip">
                    {{ tag.key }}: {{ tag.value }}
                  </mat-chip>
                }
              </div>
            </div>
          }
        </mat-card-content>
      </mat-card>
    }
  `,
  styles: [`
    .context-card {
      margin-bottom: 16px;
      background-color: rgba(33, 150, 243, 0.05);
      border-left: 4px solid #2196f3;
    }

    .transport-icon {
      background-color: rgba(33, 150, 243, 0.1);
      color: #2196f3;
    }

    .timing-info {
      display: flex;
      flex-wrap: wrap;
      gap: 8px;
      margin-bottom: 12px;
    }

    .timing-info mat-chip {
      display: flex;
      align-items: center;
      gap: 4px;
    }

    .trip-description {
      margin-bottom: 12px;
      padding: 8px;
      background-color: rgba(33, 150, 243, 0.08);
      border-radius: 4px;
    }

    .trip-tags strong {
      display: block;
      margin-bottom: 8px;
    }

    .tags-container {
      display: flex;
      flex-wrap: wrap;
      gap: 4px;
    }

    .tag-chip {
      font-size: 12px;
      min-height: 24px;
    }

    /* Mobile responsiveness */
    @media (max-width: 768px) {
      .timing-info {
        flex-direction: column;
        align-items: flex-start;
      }
    }
  `]
})
export class TrawellingContextCardComponent {
  @Input() trip: TrawellingTrip | null = null;

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

  formatDuration(minutes: number): string {
    const hours = Math.floor(minutes / 60);
    const mins = minutes % 60;
    return hours > 0 ? `${hours}h ${mins}m` : `${mins}m`;
  }
}