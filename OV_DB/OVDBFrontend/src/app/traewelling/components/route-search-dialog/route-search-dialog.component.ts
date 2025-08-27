import { Component, OnInit, inject } from '@angular/core';

import { FormsModule } from '@angular/forms';
import { MatDialogModule, MatDialogRef, MAT_DIALOG_DATA } from '@angular/material/dialog';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatInputModule } from '@angular/material/input';
import { MatProgressSpinnerModule } from '@angular/material/progress-spinner';
import { MatListModule } from '@angular/material/list';
import { MatDividerModule } from '@angular/material/divider';
import { MatSnackBar } from '@angular/material/snack-bar';
import { Router } from '@angular/router';
import { debounceTime, distinctUntilChanged } from 'rxjs/operators';
import { Subject } from 'rxjs';
import { TrawellingService } from '../../services/traewelling.service';
import { TrawellingTrip, RouteSearchResult } from '../../../models/traewelling.model';
import { TranslateModule, TranslateService } from '@ngx-translate/core';
import { TranslationService } from 'src/app/services/translation.service';
import { MatChip } from "@angular/material/chips";
import { DatePipe } from '@angular/common';

@Component({
  selector: 'app-route-search-dialog',
  standalone: true,
  imports: [
    FormsModule,
    MatDialogModule,
    MatButtonModule,
    MatIconModule,
    MatFormFieldModule,
    MatInputModule,
    MatProgressSpinnerModule,
    MatListModule,
    MatDividerModule,
    TranslateModule,
    MatChip,
    DatePipe
  ],
  template: `
    <h2 mat-dialog-title>{{'TRAEWELLING.ADD_TO_EXISTING_ROUTE' | translate}}</h2>

    <mat-dialog-content class="dialog-content">
      <!-- Trip Summary -->
      <div class="trip-summary">
        <h4>{{ 'TRAEWELLING.TRIP_CONTEXT' | translate }}</h4>
        <div class="trip-info">
          <div class="route-line">
            <strong>{{ data.trip.transport.lineName }}</strong>
            @if (getJourneyNumber()) {
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
    
      <!-- Search Section -->
      <div class="search-section">
        <h4>{{ 'TRAEWELLING.SEARCH_ROUTE' | translate }}</h4>

        <mat-form-field class="search-field" appearance="outline">
          <mat-label>{{'ROUTEDETAILS.NAME'|translate}} {{'ROUTEDETAILS.LINENUMBER'|translate}}</mat-label>
          <input
            matInput
            [(ngModel)]="searchQuery"
            (input)="onSearchInput()">
            <mat-icon matSuffix>search</mat-icon>
          </mat-form-field>
    
          <!-- Loading State -->
          @if (isSearching) {
            <div class="loading-container">
              <mat-spinner diameter="30"></mat-spinner>
              <p>{{'TRAEWELLING.SEARCHING_ROUTES' | translate}}</p>
            </div>
          }
    
          <!-- Empty State -->
          @if (!isSearching && searchQuery && routes.length === 0) {
            <div class="empty-container">
              <mat-icon class="empty-icon">search_off</mat-icon>
              <p>{{ 'TRAEWELLING.NO_ROUTES_FOUND' | translate: { query: searchQuery } }}</p>
            </div>
          }
    
          <!-- Results List -->
          @if (!isSearching && routes.length > 0) {
            <mat-selection-list
              [(ngModel)]="selectedRoutes"
              [multiple]="false">
              @for (route of routes; track route) {
                <mat-list-option
                  [value]="route"
                  class="route-option">
                    <div class="route-header" matListItemTitle>
                      <strong>{{ route.name }}</strong>
                      @if (route.routeType) {
                        <mat-chip style="margin-left:8px;" [style.background]="route.routeType.colour">{{ name(route.routeType) }}</mat-chip>
                      }
                    </div>
                    <div class="route-path" matListItemLine>
                      {{ route.from }} → {{ route.to }}
                    </div>
                    <div matListItemLine> 
                    @if (route.lineNumber) {
                      <span class="route-line">
                        <mat-icon>route</mat-icon>
                        {{ route.lineNumber }}
                      </span>
                    }
                    @if (route.operatingCompany) {
                      <span class="route-operator">
                        <mat-icon>business</mat-icon>
                        {{ route.operatingCompany }}
                      </span>
                    }
                    ({{route.firstDateTime|date:'shortDate'}})
                    </div>
                </mat-list-option>
              }
            </mat-selection-list>
          }
        </div>
      </mat-dialog-content>
    
      <mat-dialog-actions align="end">
        <button mat-button (click)="onCancel()">{{'CANCEL'|translate}}</button>
        <button
          mat-raised-button
          color="primary"
          (click)="onSelectRoute()"
          [disabled]="!selectedRoute || isCreating">
          @if (isCreating) {
            <mat-spinner diameter="16" style="margin-right: 8px;"></mat-spinner>
          }
          {{( isCreating ? 'TRAEWELLING.CREATING_INSTANCE' : 'ROUTEINSTANCESEDIT.NEWTITLE') | translate }}
        </button>
      </mat-dialog-actions>
    `,
  styleUrls: ['./route-search-dialog.component.scss']
})
export class RouteSearchDialogComponent implements OnInit {
  trawellingService = inject(TrawellingService);
  translateService = inject(TranslateService);
  translationService = inject(TranslationService);
  private snackBar = inject(MatSnackBar);
  private router = inject(Router);
  private dialogRef = inject<MatDialogRef<RouteSearchDialogComponent>>(MatDialogRef);
  data = inject<{
    trip: TrawellingTrip;
  }>(MAT_DIALOG_DATA);

  searchQuery = '';
  routes: RouteSearchResult[] = [];
  selectedRoutes: RouteSearchResult[] | null = null;

  get selectedRoute() {
    return this.selectedRoutes && this.selectedRoutes.length > 0 ? this.selectedRoutes[0] : null;
  }
  isSearching = false;
  isCreating = false;

  private searchSubject = new Subject<string>();

  ngOnInit() {
    // Set up debounced search
    this.searchSubject.pipe(
      debounceTime(300),
      distinctUntilChanged()
    ).subscribe((query: string) => {
      if (query.trim()) {
        this.performSearch(query.trim());
      } else {
        this.routes = [];
      }
    });

    // Initialize search with trip origin/destination
    this.searchQuery = this.data.trip.transport.origin.name;
    this.onSearchInput();
  }

  getJourneyNumber(): string {
    return this.data.trip.transport.journeyNumber || '';
  }

  onSearchInput() {
    this.searchSubject.next(this.searchQuery);
  }

  private async performSearch(query: string) {
    this.isSearching = true;
    try {
      this.routes = await this.trawellingService.searchRoutes(query);
    } catch (error) {
      this.snackBar.open(this.translateService.instant('TRAEWELLING.ERROR_SEARCHING_ROUTES'), this.translateService.instant('CLOSE'), { duration: 5000 });
    } finally {
      this.isSearching = false;
    }
  }

  async onSelectRoute() {
    if (!this.selectedRoute) return;

    this.isCreating = true;

    const tripContext = this.trawellingService.getTripContextForRouteCreation(this.data.trip);
    sessionStorage.setItem('traewellingTripContext', JSON.stringify(tripContext));
    console.log(this.data.trip, this.selectedRoute);
    // Close dialog and navigate
    this.dialogRef.close({ routeCreated: true });
    this.router.navigate(['/admin/routes/instances', this.selectedRoute.routeId], {
      queryParams: {
        traewellingTripId: this.data.trip.id,
      }
    });
    this.isCreating = false;
  }

  onCancel() {
    this.dialogRef.close();
  }

  name(routeType: {
    name: string;
    nameNL: string;
  }): string {
    return this.translationService.getNameForItem(routeType);
  }
}