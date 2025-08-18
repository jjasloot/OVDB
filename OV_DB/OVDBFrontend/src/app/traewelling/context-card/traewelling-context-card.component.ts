import { Component, Input } from '@angular/core';
import { CommonModule } from '@angular/common';
import { MatCardModule } from '@angular/material/card';
import { MatChipsModule } from '@angular/material/chips';
import { MatIconModule } from '@angular/material/icon';
import { TranslateModule } from '@ngx-translate/core';

interface TrawellingTripData {
  origin?: string;
  destination?: string;
  line?: string;
  number?: string;
  startTime?: string;
  endTime?: string;
  body?: string;
  tags?: Array<{ key: string; value: string }>;
}

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
    @if (trawellingTripData) {
      <mat-card class="traewelling-context">
        <mat-card-header>
          <mat-card-title>
            <div class="context-header">
              <mat-icon>train</mat-icon>
              <span>{{ 'TRAEWELLING.TRIP_CONTEXT' | translate }}</span>
            </div>
          </mat-card-title>
          <mat-card-subtitle>
            {{ trawellingTripData.origin }} → {{ trawellingTripData.destination }}
          </mat-card-subtitle>
        </mat-card-header>
        
        <mat-card-content>
          @if (trawellingTripData.line) {
            <div class="info-row">
              <mat-icon>directions_transit</mat-icon>
              <span>
                <strong>{{ trawellingTripData.line }}</strong>
                @if (trawellingTripData.number) {
                  {{ trawellingTripData.number }}
                }
              </span>
            </div>
          }
          
          @if (trawellingTripData.startTime || trawellingTripData.endTime) {
            <div class="timing-info">
              @if (trawellingTripData.startTime) {
                <mat-chip>
                  <mat-icon>schedule</mat-icon>
                  {{ 'TRAEWELLING.DEPARTURE' | translate }}: {{ trawellingTripData.startTime | date:'short' }}
                </mat-chip>
              }
              @if (trawellingTripData.endTime) {
                <mat-chip>
                  <mat-icon>schedule</mat-icon>
                  {{ 'TRAEWELLING.ARRIVAL' | translate }}: {{ trawellingTripData.endTime | date:'short' }}
                </mat-chip>
              }
            </div>
          }

          @if (trawellingTripData.body) {
            <div class="trip-description">
              <mat-icon>description</mat-icon>
              <span>{{ trawellingTripData.body }}</span>
            </div>
          }

          @if (trawellingTripData.tags && trawellingTripData.tags.length > 0) {
            <div class="trip-tags">
              <h4>{{ 'TRAEWELLING.TAGS' | translate }}:</h4>
              <div class="tags-container">
                @for (tag of trawellingTripData.tags; track tag.key) {
                  <mat-chip class="tag-chip">
                    <span class="tag-key">{{ tag.key }}:</span>
                    <span class="tag-value">{{ tag.value }}</span>
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
    .traewelling-context {
      margin-bottom: 24px;
      background: linear-gradient(135deg, #e3f2fd 0%, #f1f8e9 100%);
      border-left: 4px solid #1976d2;
    }

    .context-header {
      display: flex;
      align-items: center;
      gap: 8px;
    }

    .context-header mat-icon {
      color: #1976d2;
    }

    .info-row {
      display: flex;
      align-items: center;
      gap: 8px;
      margin-bottom: 12px;
    }

    .info-row mat-icon {
      color: #1976d2;
      font-size: 18px;
      width: 18px;
      height: 18px;
    }

    .info-row span {
      font-size: 14px;
    }

    .timing-info {
      margin: 12px 0;
      display: flex;
      flex-wrap: wrap;
      gap: 8px;
    }

    .timing-info mat-chip {
      display: flex;
      align-items: center;
      gap: 4px;
      font-size: 12px;
    }

    .timing-info mat-chip mat-icon {
      font-size: 14px;
      width: 14px;
      height: 14px;
    }

    .trip-description {
      display: flex;
      align-items: flex-start;
      gap: 8px;
      margin: 12px 0;
      padding: 12px;
      background: rgba(255, 152, 0, 0.1);
      border-radius: 8px;
      border-left: 3px solid #ff9800;
    }

    .trip-description mat-icon {
      color: #ff9800;
      margin-top: 2px;
      flex-shrink: 0;
      font-size: 16px;
      width: 16px;
      height: 16px;
    }

    .trip-description span {
      line-height: 1.4;
      font-size: 14px;
    }

    .trip-tags {
      margin: 12px 0;
    }

    .trip-tags h4 {
      font-size: 14px;
      font-weight: 500;
      margin-bottom: 8px;
      color: #1976d2;
    }

    .tags-container {
      display: flex;
      flex-wrap: wrap;
      gap: 8px;
    }

    .tag-chip {
      font-size: 12px;
      background: rgba(25, 118, 210, 0.1);
      color: #1976d2;
    }

    .tag-key {
      font-weight: 500;
      margin-right: 4px;
    }

    .tag-value {
      opacity: 0.8;
    }
  `]
})
export class TrawellingContextCardComponent {
  @Input() trawellingTripData: TrawellingTripData | null = null;
}