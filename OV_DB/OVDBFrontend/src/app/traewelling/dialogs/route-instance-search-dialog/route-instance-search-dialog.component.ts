import { Component, Inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { MatDialogModule, MAT_DIALOG_DATA, MatDialogRef } from '@angular/material/dialog';
import { MatButtonModule } from '@angular/material/button';
import { MatListModule } from '@angular/material/list';
import { MatIconModule } from '@angular/material/icon';
import { MatChipsModule } from '@angular/material/chips';
import { MatProgressSpinnerModule } from '@angular/material/progress-spinner';
import { TranslateModule } from '@ngx-translate/core';
import { TrawellingTrip, RouteInstanceSearchResult } from '../../../models/traewelling.model';
import { TrawellingService } from '../../services/traewelling.service';

@Component({
  selector: 'app-route-instance-search-dialog',
  standalone: true,
  imports: [
    CommonModule,
    MatDialogModule,
    MatButtonModule,
    MatListModule,
    MatIconModule,
    MatChipsModule,
    MatProgressSpinnerModule,
    TranslateModule
  ],
  template: `
    <h2 mat-dialog-title>{{ 'TRAEWELLING.SELECT_ROUTE_INSTANCE' | translate }}</h2>
    
    <mat-dialog-content>
      @if (loading) {
        <div class="loading-container">
          <mat-spinner diameter="32"></mat-spinner>
          <span>{{ 'TRAEWELLING.SEARCHING_ROUTE_INSTANCES' | translate }}</span>
        </div>
      } @else if (routeInstances.length > 0) {
        <mat-list class="route-instances-list">
          @for (instance of routeInstances; track instance.id) {
            <mat-list-item 
              (click)="selectRouteInstance(instance)" 
              class="route-instance-item"
              [class.disabled]="instance.hasTraewellingLink">
              <mat-icon matListItemIcon>
                {{ instance.hasTraewellingLink ? 'link' : 'train' }}
              </mat-icon>
              <div matListItemTitle>
                {{ instance.routeName || (instance.from + ' - ' + instance.to) }}
              </div>
              <div matListItemLine>
                {{ instance.from }} → {{ instance.to }}
              </div>
              <div matListItemLine class="instance-details">
                @if (instance.startTime) {
                  <mat-chip class="time-chip">
                    <mat-icon>schedule</mat-icon>
                    {{ instance.startTime | date:'short' }}
                  </mat-chip>
                }
                @if (instance.durationHours) {
                  <mat-chip class="duration-chip">
                    <mat-icon>timer</mat-icon>
                    {{ formatDuration(instance.durationHours) }}
                  </mat-chip>
                }
                @if (instance.hasTraewellingLink) {
                  <mat-chip class="linked-chip" color="accent">
                    <mat-icon>link</mat-icon>
                    {{ 'TRAEWELLING.ALREADY_LINKED' | translate }}
                  </mat-chip>
                }
              </div>
            </mat-list-item>
          }
        </mat-list>
      } @else {
        <div class="no-results">
          <mat-icon>no_transfer</mat-icon>
          <span>{{ 'TRAEWELLING.NO_ROUTE_INSTANCES_FOUND' | translate }}</span>
          <p>Try checking for route instances on a different date or create a new route instance.</p>
        </div>
      }
    </mat-dialog-content>

    <mat-dialog-actions align="end">
      <button mat-button (click)="close()">{{ 'CANCEL' | translate }}</button>
    </mat-dialog-actions>
  `,
  styles: [`
    .loading-container {
      display: flex;
      flex-direction: column;
      align-items: center;
      gap: 16px;
      padding: 24px;
    }

    .route-instances-list {
      max-height: 400px;
      overflow-y: auto;
    }

    .route-instance-item {
      cursor: pointer;
      border-radius: 8px;
      margin-bottom: 8px;
      padding: 12px;
      border: 1px solid rgba(0, 0, 0, 0.1);
    }

    .route-instance-item:hover:not(.disabled) {
      background-color: rgba(33, 150, 243, 0.08);
      border-color: rgba(33, 150, 243, 0.3);
    }

    .route-instance-item.disabled {
      opacity: 0.6;
      cursor: not-allowed;
    }

    .instance-details {
      display: flex;
      flex-wrap: wrap;
      gap: 4px;
      margin-top: 8px;
    }

    .time-chip,
    .duration-chip {
      display: flex;
      align-items: center;
      gap: 4px;
      font-size: 12px;
      min-height: 24px;
    }

    .linked-chip {
      background-color: rgba(255, 152, 0, 0.2);
    }

    .no-results {
      display: flex;
      flex-direction: column;
      align-items: center;
      gap: 16px;
      padding: 48px 24px;
      text-align: center;
      opacity: 0.6;
    }

    .no-results mat-icon {
      font-size: 64px;
      width: 64px;
      height: 64px;
    }

    .no-results p {
      margin: 0;
      font-size: 14px;
      max-width: 300px;
    }

    /* Mobile responsiveness */
    @media (max-width: 768px) {
      .instance-details {
        flex-direction: column;
        align-items: flex-start;
      }
    }
  `]
})
export class RouteInstanceSearchDialogComponent {
  routeInstances: RouteInstanceSearchResult[] = [];
  loading = true;

  constructor(
    private dialogRef: MatDialogRef<RouteInstanceSearchDialogComponent>,
    @Inject(MAT_DIALOG_DATA) public data: { trip: TrawellingTrip },
    private trawellingService: TrawellingService
  ) {
    this.searchRouteInstances();
  }

  async searchRouteInstances() {
    try {
      this.routeInstances = await this.trawellingService.searchRouteInstances(this.data.trip);
    } catch (error) {
      console.error('Error searching route instances:', error);
      this.routeInstances = [];
    } finally {
      this.loading = false;
    }
  }

  async selectRouteInstance(instance: RouteInstanceSearchResult) {
    if (instance.hasTraewellingLink) return;

    try {
      const result = await this.trawellingService.linkToRouteInstance({
        statusId: this.data.trip.id,
        routeInstanceId: instance.id
      });

      this.dialogRef.close({ 
        success: result.success, 
        routeInstance: instance,
        message: result.message 
      });
    } catch (error) {
      console.error('Error linking to route instance:', error);
      this.dialogRef.close({ 
        success: false, 
        message: 'Error linking trip to route instance' 
      });
    }
  }

  formatDuration(hours: number): string {
    const h = Math.floor(hours);
    const m = Math.round((hours - h) * 60);
    return h > 0 ? `${h}h ${m}m` : `${m}m`;
  }

  close() {
    this.dialogRef.close();
  }
}