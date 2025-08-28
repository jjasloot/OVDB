import { Component, inject, Input } from '@angular/core';

import { MatCardModule } from '@angular/material/card';
import { MatChipsModule } from '@angular/material/chips';
import { MatIconModule } from '@angular/material/icon';
import { TrawellingTransportCategory, TrawellingTripContext } from '../../models/traewelling.model';
import { TrawellingService } from '../services/traewelling.service';
import { MatButtonModule } from '@angular/material/button';
import { TranslateModule } from '@ngx-translate/core';

@Component({
  selector: 'app-traewelling-context-card',
  standalone: true,
  imports: [
    MatCardModule,
    MatChipsModule,
    MatIconModule,
    MatButtonModule,
    TranslateModule
  ],
  template: `
    @if (tripContext) {
      <mat-card class="context-card">
        <mat-card-header>
          <mat-card-title class="context-title">
            <div class="transport-icon" [style.background-color]="transportColor()">
              <mat-icon>{{ transportIcon() }}</mat-icon>
            </div>
            Träwelling
          </mat-card-title>
        </mat-card-header>
        <mat-card-content>
          <!-- Trip Route -->
          <div class="trip-route">
            <div class="line-info">
              <strong>{{ tripContext.lineNumber }}</strong>
              @if (tripContext.journeyNumber) {
                <span> • {{ tripContext.journeyNumber }}</span>
              }
            </div>
            <div class="stations">
              {{ tripContext.originName }} → {{ tripContext.destinationName }}
            </div>
          </div>
          <!-- Timing -->
          @if (tripContext.departureTime || tripContext.arrivalTime) {
            <div class="trip-timing">
              <div class="timing-item">
                <mat-icon>schedule</mat-icon>
                <span>{{ formatTime(tripContext.departureTime) }} - {{ formatTime(tripContext.arrivalTime) }}</span>
              </div>
              <div class="duration">
                <mat-icon>access_time</mat-icon>
                <span>{{ formatDuration(tripContext.duration) }} • {{ formatDistance(tripContext.distance) }}</span>
              </div>
            </div>
          }
          <!-- Description -->
          @if (tripContext.description) {
            <div class="trip-description">
              <p>{{ tripContext.description }}</p>
            </div>
          }
          <!-- Tags -->
          @if (tripContext.tags.length > 0) {
            <div class="trip-tags">
                @for (tag of tripContext.tags; track tag) {
                  <mat-chip>
                    <strong>{{ tag.key }}:</strong> {{ tag.value }}
                  </mat-chip>
                }
            </div>
          }
        </mat-card-content>
        <mat-card-actions align="end">
          <button mat-button (click)="closeCard()">{{'CLOSE'|translate}}</button>
        </mat-card-actions>
      </mat-card>
    }
    `,
  styleUrls: ['./traewelling-context-card.component.scss']
})
export class TrawellingContextCardComponent {
  traewellingService = inject(TrawellingService);
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

  transportIcon() {
    return this.traewellingService.getTransportIcon(this.tripContext.transportCategory);
  }

  transportColor() {
    return this.traewellingService.getTransportColor(this.tripContext.transportCategory);
  }

  closeCard() {
    sessionStorage.removeItem('traewellingTripContext');
    this.tripContext = null;
  }
}