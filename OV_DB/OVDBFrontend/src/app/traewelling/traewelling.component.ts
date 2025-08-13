import { Component, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { Router } from '@angular/router';
import { MatSnackBar } from '@angular/material/snack-bar';
import { TranslateModule, TranslateService } from '@ngx-translate/core';
import { MatCard, MatCardContent, MatCardHeader, MatCardTitle, MatCardSubtitle, MatCardActions } from '@angular/material/card';
import { MatButton } from '@angular/material/button';
import { MatIcon } from '@angular/material/icon';
import { MatProgressSpinner } from '@angular/material/progress-spinner';
import { MatPaginator, PageEvent } from '@angular/material/paginator';
import { MatChipSet, MatChip } from '@angular/material/chips';

import { ApiService } from '../services/api.service';
import {
  TrawellingConnectionStatus,
  TrawellingStatus,
  TrawellingStatusesResponse,
  TrawellingStats,
  RouteInstanceSearchResult,
  TrawellingHafasTravelType
} from '../models/traewelling.model';

@Component({
  selector: 'app-traewelling',
  templateUrl: './traewelling.component.html',
  styleUrls: ['./traewelling.component.scss'],
  imports: [
    CommonModule,
    TranslateModule,
    MatCard,
    MatCardContent,
    MatCardHeader,
    MatCardTitle,
    MatCardSubtitle,
    MatCardActions,
    MatButton,
    MatIcon,
    MatProgressSpinner,
    MatChipSet,
    MatChip
  ]
})
export class TrawellingComponent implements OnInit {
  connectionStatus: TrawellingConnectionStatus | null = null;
  unimportedTrips: TrawellingStatus[] = [];
  loading = false;
  tripsLoading = false;
  statsLoading = false;

  // Pagination
  currentPage = 1;

  // Search and filters
  searchTerm = '';
  existingRouteInstances: RouteInstanceSearchResult[] = [];
  searchingRouteInstances = false;
  searchTrip: TrawellingStatus | null = null;

  constructor(
    public apiService: ApiService,
    public router: Router,
    private snackBar: MatSnackBar,
    private translateService: TranslateService
  ) { }

  ngOnInit(): void {
    this.checkConnection();
  }

  checkConnection(): void {
    this.loading = true;
    this.apiService.getTrawellingStatus().subscribe({
      next: (status) => {
        this.connectionStatus = status;
        if (status.connected) {
          this.loadUnimportedTrips();
        }
        this.loading = false;
      },
      error: (error) => {
        console.error('Error checking Träwelling connection:', error);
        this.loading = false;
        this.router.navigate(['/profile']);
      }
    });
  }

  loadUnimportedTrips(page: number = 1): void {
    this.tripsLoading = true;
    this.currentPage = page;
    this.apiService.getTrawellingUnimported(this.currentPage).subscribe({
      next: (response: TrawellingStatusesResponse) => {
        this.unimportedTrips = response.data;
        this.currentPage = response.meta.current_page;
        this.tripsLoading = false;
      },
      error: (error) => {
        console.error('Error loading unimported trips:', error);
        this.showMessage('TRAEWELLING.ERROR_LOADING_TRIPS');
        this.tripsLoading = false;
      }
    });

  }

  loadMoreTrips() {
    this.currentPage++;
    this.apiService.getTrawellingUnimported(this.currentPage).subscribe({
      next: (response: TrawellingStatusesResponse) => {
        this.unimportedTrips = this.unimportedTrips.concat(response.data);
        this.currentPage = response.meta.current_page;
        this.tripsLoading = false;
      },
      error: (error) => {
        console.error('Error loading unimported trips:', error);
        this.showMessage('TRAEWELLING.ERROR_LOADING_TRIPS');
        this.tripsLoading = false;
      }
    });
  }


  onPageChange(event: PageEvent): void {
    this.loadUnimportedTrips(event.pageIndex + 1);
  }

  ignoreTrip(trip: TrawellingStatus): void {
    this.apiService.ignoreTrawellingStatus(trip.id).subscribe({
      next: (response) => {
        if (response.success) {
          this.showMessage('TRAEWELLING.TRIP_IGNORED');
          // Remove the ignored trip from the list
          this.unimportedTrips = this.unimportedTrips.filter(t => t.id !== trip.id);
        } else {
          this.showMessage('TRAEWELLING.ERROR_IGNORING_TRIP');
        }
      },
      error: (error) => {
        console.error('Error ignoring trip:', error);
        this.showMessage('TRAEWELLING.ERROR_IGNORING_TRIP');
      }
    });
  }

  searchExistingRoutes(trip: TrawellingStatus): void {
    this.searchTrip = trip;
    this.searchingRouteInstances = true;
    this.existingRouteInstances = [];

    // Parse the trip departure date from createdAt or train origin departure
    const tripDate = trip.train?.origin?.departure
      ? new Date(trip.train.origin.departure)
      : new Date(trip.createdAt);
    const dateString = tripDate.toISOString().split('T')[0]; // YYYY-MM-DD format

    // Create search query from origin and destination
    const searchQuery = trip.train?.origin?.name && trip.train?.destination?.name
      ? `${trip.train.origin.name} ${trip.train.destination.name}`
      : '';

    this.apiService.searchRouteInstances(dateString, searchQuery).subscribe({
      next: (routeInstances) => {
        this.existingRouteInstances = routeInstances;
        this.searchingRouteInstances = false;

        if (routeInstances.length === 0) {
          this.showMessage('TRAEWELLING.NO_ROUTE_INSTANCES_FOUND');
        }
      },
      error: (error) => {
        console.error('Error searching RouteInstances:', error);
        this.searchingRouteInstances = false;
        this.showMessage('TRAEWELLING.ERROR_SEARCHING_ROUTES');
      }
    });
  }

  linkToExistingRouteInstance(trip: TrawellingStatus, routeInstance: RouteInstanceSearchResult): void {
    const linkRequest = {
      statusId: trip.id,
      routeInstanceId: routeInstance.id
    };

    this.apiService.linkToRouteInstance(linkRequest).subscribe({
      next: (response) => {
        if (response.success) {
          this.showMessage('TRAEWELLING.TRIP_LINKED');
          // Remove the linked trip from the unimported list
          this.unimportedTrips = this.unimportedTrips.filter(t => t.id !== trip.id);
          // Clear the search results
          this.existingRouteInstances = [];
          this.searchTrip = null;
        } else {
          this.showMessage('TRAEWELLING.ERROR_LINKING_TRIP');
        }
      },
      error: (error) => {
        console.error('Error linking trip:', error);
        this.showMessage('TRAEWELLING.ERROR_LINKING_TRIP');
      }
    });
  }

  refreshTrips(): void {
    this.currentPage = 1;
    this.loadUnimportedTrips(this.currentPage);
  }

  formatDuration(minutes?: number): string {
    if (!minutes) return '';
    const hours = Math.floor(minutes / 60);
    const mins = Math.floor(minutes % 60);
    return hours > 0 ? `${hours}h ${mins}m` : `${mins}m`;
  }

  formatDistance(meters?: number): string {
    if (!meters) return '';
    return meters > 1000 ? `${(meters / 1000).toFixed(1)} km` : `${meters} m`;
  }

  getCategoryName(category: TrawellingHafasTravelType): string {
    return 'TRAEWELLING.CATEGORY_' + category.toUpperCase();
  }

  isDelayed(planned: string | null | undefined, actual: string | null | undefined): boolean {
    if (!planned || !actual) return false;
    return new Date(actual).getTime() > new Date(planned).getTime();
  }

  private showMessage(messageKey: string): void {
    this.translateService.get(messageKey).subscribe(message => {
      this.snackBar.open(message, '', { duration: 3000 });
    });
  }
}