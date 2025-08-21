import { Component, inject, OnInit } from '@angular/core';

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
import { TranslateModule, TranslateService } from '@ngx-translate/core';
import { DatePipe } from '@angular/common';

@Component({
  selector: 'app-route-instance-search-dialog',
  standalone: true,
  imports: [
    FormsModule,
    MatDialogModule,
    MatButtonModule,
    MatIconModule,
    MatProgressSpinnerModule,
    MatListModule,
    MatDividerModule,
    TranslateModule,
    DatePipe
  ],
  template: `
    <h2 mat-dialog-title>{{'TRAEWELLING.CONNECT_TO_INSTANCE'|translate}}</h2>
    
    <mat-dialog-content class="dialog-content">
      <!-- Trip Summary -->
      <div class="trip-summary">
        <h4>{{ 'TRAEWELLING.TRIP_CONTEXT' | translate }}</h4>
        <div class="trip-info">
          <div class="route-line">
            <strong>{{ data.trip.transport.lineName }}</strong>
            @if(getJourneyNumber()){
              <span> • {{ getJourneyNumber() }}</span>
            }
          </div>
          <div class="route-stations">
            {{ data.trip.transport.origin.name }} → {{ data.trip.transport.destination.name }}
          </div>
          <div class="route-times">
            {{ data.trip.transport.origin.departureScheduled | date:'shortTime' }} -
            {{ data.trip.transport.destination.arrivalScheduled | date:'shortTime' }}
          </div>
        </div>
      </div>
    
      <mat-divider></mat-divider>
    
      <!-- Search Results -->
      <div class="search-section">
        <h4>{{'TRAEWELLING.SEARCH_ROUTE_INSTANCES'|translate}}</h4>
    
        <!-- Loading State -->
        @if (isLoading) {
          <div class="loading-container">
            <mat-spinner diameter="30"></mat-spinner>
            <p>{{'TRAEWELLING.SEARCHING_ROUTE_INSTANCES'|translate}}</p>
          </div>
        }
    
        <!-- Empty State -->
        @if (!isLoading && routeInstances.length === 0) {
          <div class="empty-container">
            <mat-icon class="empty-icon">search_off</mat-icon>
            <p>{{'TRAEWELLING.NO_ROUTE_INSTANCES_FOUND'|translate}}</p>
          </div>
        }
    
        <!-- Results List -->
        @if (!isLoading && routeInstances.length > 0) {
          <mat-selection-list
            [(ngModel)]="selectedRouteInstances"
            [multiple]="false">
            @for (instance of routeInstances; track instance) {
              <mat-list-option
                [value]="instance"
                class="route-instance-option">
                  <div class="instance-header" matListItemTitle>
                    <strong>{{ instance.routeName }}</strong>
                    <span class="instance-date">{{ formatInstanceDate(instance.date) }}</span>
                  </div>
                  <div class="instance-route" matListItemLine>
                    {{ instance.from }} → {{ instance.to }}
                  </div>
                  @if(instance.startTime && instance.endTime){
                    <div class="instance-times" matListItemLine>
                      <mat-icon>schedule</mat-icon>
                      {{ instance.startTime|date:'shortTime' }} - {{ instance.endTime|date:'shortTime' }}
                      @if(instance.durationHours){
                        <span  class="duration">
                          ({{ trawellingService.formatDurationHours(instance.durationHours) }})
                        </span>
                      }
                    </div>
                  }
              </mat-list-option>
            }
          </mat-selection-list>
        }
      </div>
    </mat-dialog-content>
    
    <mat-dialog-actions align="end">
      <button mat-button (click)="onCancel()">{{'CANCEL' | translate}}</button>
      <button
        mat-raised-button
        color="primary"
        (click)="onLink()"
        [disabled]="!selectedRouteInstance || isLinking">
        @if (isLinking) {
          <mat-spinner diameter="16" style="margin-right: 8px;"></mat-spinner>
        }
        {{ (isLinking ? 'TRAEWELLING.LINKING' : 'TRAEWELLING.LINK_TO_EXISTING') | translate }}
      </button>
    </mat-dialog-actions>
    `,
  styleUrls: ['./route-instance-search-dialog.component.scss']
})
export class RouteInstanceSearchDialogComponent implements OnInit {
  trawellingService = inject(TrawellingService);
  private snackBar = inject(MatSnackBar);
  private dialogRef = inject<MatDialogRef<RouteInstanceSearchDialogComponent>>(MatDialogRef);
  data = inject<{
    trip: TrawellingTrip;
  }>(MAT_DIALOG_DATA);

  routeInstances: RouteInstanceSearchResult[] = [];
  selectedRouteInstances: RouteInstanceSearchResult[] = [];
  get selectedRouteInstance(): RouteInstanceSearchResult | null {
    return this.selectedRouteInstances.length > 0 ? this.selectedRouteInstances[0] : null;
  }
  translateService = inject(TranslateService);
  isLoading = true;
  isLinking = false;

  async ngOnInit() {
    await this.searchRouteInstances();
  }

  getJourneyNumber(): string {
    return this.data.trip.transport.journeyNumber || '';
  }

  formatInstanceDate(dateString: string): string {
    return this.trawellingService.formatDate(dateString);
  }

  private async searchRouteInstances() {
    try {
      this.routeInstances = await this.trawellingService.searchRouteInstances(this.data.trip);
    } catch (error) {
      this.snackBar.open(this.translateService.instant('TRAEWELLING.ERROR_SEARCHING_ROUTE_INSTANCES'), this.translateService.instant('CLOSE'), { duration: 5000 });
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
        this.snackBar.open(this.translateService.instant('TRAEWELLING.TRIP_LINKED'), this.translateService.instant('CLOSE'), { duration: 3000 });
        this.dialogRef.close({ success: true, routeInstance: response.routeInstance });
      } else {
        this.snackBar.open(response.message || this.translateService.instant('TRAEWELLING.ERROR_LINKING_TRIP'), this.translateService.instant('CLOSE'), { duration: 5000 });
      }
    } catch (error) {
      this.snackBar.open(this.translateService.instant('TRAEWELLING.ERROR_LINKING_TRIP'), this.translateService.instant('CLOSE'), { duration: 5000 });
    } finally {
      this.isLinking = false;
    }
  }

  onCancel() {
    this.dialogRef.close();
  }
}