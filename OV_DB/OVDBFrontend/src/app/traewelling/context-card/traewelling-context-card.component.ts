import { Component, Input } from '@angular/core';
import { CommonModule } from '@angular/common';
import { MatCardModule } from '@angular/material/card';
import { MatChipsModule } from '@angular/material/chips';
import { MatIconModule } from '@angular/material/icon';
import { TrawellingTripContext } from '../../models/traewelling.model';

@Component({
  selector: 'app-traewelling-context-card',
  standalone: true,
  imports: [
    CommonModule,
    MatCardModule,
    MatChipsModule,
    MatIconModule
  ],
  template: `
    <mat-card *ngIf="tripContext" class="context-card">
      <mat-card-header>
        <mat-card-title class="context-title">
          <mat-icon>train</mat-icon>
          Träwelling Trip Context
        </mat-card-title>
      </mat-card-header>
      
      <mat-card-content>
        <!-- Trip Route -->
        <div class="trip-route">
          <div class="line-info">
            <strong>{{ tripContext.lineNumber }}</strong>
            <span *ngIf="tripContext.journeyNumber"> • {{ tripContext.journeyNumber }}</span>
          </div>
          <div class="stations">
            {{ tripContext.originName }} → {{ tripContext.destinationName }}
          </div>
        </div>

        <!-- Timing -->
        <div class="trip-timing" *ngIf="tripContext.departureTime || tripContext.arrivalTime">
          <div class="timing-item">
            <mat-icon>schedule</mat-icon>
            <span>{{ formatTime(tripContext.departureTime) }} - {{ formatTime(tripContext.arrivalTime) }}</span>
          </div>
          <div class="duration">
            <mat-icon>access_time</mat-icon>
            <span>{{ formatDuration(tripContext.duration) }} • {{ formatDistance(tripContext.distance) }}</span>
          </div>
        </div>

        <!-- Description -->
        <div class="trip-description" *ngIf="tripContext.description">
          <p>{{ tripContext.description }}</p>
        </div>

        <!-- Tags -->
        <div class="trip-tags" *ngIf="tripContext.tags.length > 0">
          <mat-chip-listbox>
            <mat-chip-option *ngFor="let tag of tripContext.tags" disabled>
              <strong>{{ tag.key }}:</strong> {{ tag.value }}
            </mat-chip-option>
          </mat-chip-listbox>
        </div>
      </mat-card-content>
    </mat-card>
  `,
  styleUrls: ['./traewelling-context-card.component.scss']
})
export class TrawellingContextCardComponent {
  @Input() tripContext: TrawellingTripContext | null = null;

  formatTime(isoString?: string): string {
    if (!isoString) return '';
    const date = new Date(isoString);
    return date.toLocaleTimeString('nl-NL', { 
      hour: '2-digit', 
      minute: '2-digit' 
    });
  }

  formatDuration(minutes: number): string {
    const hours = Math.floor(minutes / 60);
    const mins = minutes % 60;
    if (hours > 0) {
      return `${hours}h ${mins}m`;
    }
    return `${mins}m`;
  }

  formatDistance(meters: number): string {
    if (meters >= 1000) {
      return `${(meters / 1000).toFixed(1)} km`;
    }
    return `${meters} m`;
  }
}