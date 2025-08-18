import { Component, Inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormControl, ReactiveFormsModule } from '@angular/forms';
import { MatDialogModule, MAT_DIALOG_DATA, MatDialogRef } from '@angular/material/dialog';
import { MatButtonModule } from '@angular/material/button';
import { MatInputModule } from '@angular/material/input';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatListModule } from '@angular/material/list';
import { MatIconModule } from '@angular/material/icon';
import { MatProgressSpinnerModule } from '@angular/material/progress-spinner';
import { TranslateModule } from '@ngx-translate/core';
import { debounceTime, distinctUntilChanged, switchMap } from 'rxjs/operators';
import { TrawellingTrip } from '../../../models/traewelling.model';
import { TrawellingService } from '../../services/traewelling.service';

@Component({
  selector: 'app-route-search-dialog',
  standalone: true,
  imports: [
    CommonModule,
    ReactiveFormsModule,
    MatDialogModule,
    MatButtonModule,
    MatInputModule,
    MatFormFieldModule,
    MatListModule,
    MatIconModule,
    MatProgressSpinnerModule,
    TranslateModule
  ],
  template: `
    <h2 mat-dialog-title>{{ 'TRAEWELLING.SEARCH_EXISTING_ROUTE' | translate }}</h2>
    
    <mat-dialog-content>
      <mat-form-field class="search-field" appearance="outline">
        <mat-label>{{ 'TRAEWELLING.SEARCH_ROUTE' | translate }}</mat-label>
        <input matInput [formControl]="searchControl" placeholder="Start typing route name...">
        <mat-icon matSuffix>search</mat-icon>
      </mat-form-field>

      @if (searching) {
        <div class="loading-container">
          <mat-spinner diameter="32"></mat-spinner>
          <span>Searching routes...</span>
        </div>
      } @else if (routes.length > 0) {
        <mat-list class="routes-list">
          @for (route of routes; track route.id) {
            <mat-list-item (click)="selectRoute(route)" class="route-item">
              <mat-icon matListItemIcon>route</mat-icon>
              <div matListItemTitle>{{ route.name }}</div>
              <div matListItemLine>{{ route.from }} → {{ route.to }}</div>
            </mat-list-item>
          }
        </mat-list>
      } @else if (searchControl.value && !searching) {
        <div class="no-results">
          <mat-icon>search_off</mat-icon>
          <span>{{ 'TRAEWELLING.NO_ROUTES_FOUND' | translate }}</span>
        </div>
      }
    </mat-dialog-content>

    <mat-dialog-actions align="end">
      <button mat-button (click)="close()">{{ 'CANCEL' | translate }}</button>
    </mat-dialog-actions>
  `,
  styles: [`
    .search-field {
      width: 100%;
      margin-bottom: 16px;
    }

    .loading-container {
      display: flex;
      flex-direction: column;
      align-items: center;
      gap: 16px;
      padding: 24px;
    }

    .routes-list {
      max-height: 300px;
      overflow-y: auto;
    }

    .route-item {
      cursor: pointer;
      border-radius: 8px;
      margin-bottom: 4px;
    }

    .route-item:hover {
      background-color: rgba(0, 0, 0, 0.04);
    }

    .no-results {
      display: flex;
      flex-direction: column;
      align-items: center;
      gap: 16px;
      padding: 24px;
      opacity: 0.6;
    }

    .no-results mat-icon {
      font-size: 48px;
      width: 48px;
      height: 48px;
    }
  `]
})
export class RouteSearchDialogComponent {
  searchControl = new FormControl('');
  routes: any[] = [];
  searching = false;

  constructor(
    private dialogRef: MatDialogRef<RouteSearchDialogComponent>,
    @Inject(MAT_DIALOG_DATA) public data: { trip: TrawellingTrip; action: string },
    private trawellingService: TrawellingService
  ) {
    // Setup search with debouncing
    this.searchControl.valueChanges.pipe(
      debounceTime(300),
      distinctUntilChanged(),
      switchMap(async (query) => {
        if (!query || query.length < 2) {
          this.routes = [];
          return [];
        }
        
        this.searching = true;
        try {
          return await this.trawellingService.searchRoutes(query);
        } catch (error) {
          console.error('Error searching routes:', error);
          return [];
        } finally {
          this.searching = false;
        }
      })
    ).subscribe(routes => {
      this.routes = routes;
    });
  }

  selectRoute(route: any) {
    // Navigate to create route instance for this route with trip data
    this.dialogRef.close({ 
      success: true, 
      route: route,
      tripData: this.trawellingService.getTripDataForRouteCreation(this.data.trip)
    });
  }

  close() {
    this.dialogRef.close();
  }
}