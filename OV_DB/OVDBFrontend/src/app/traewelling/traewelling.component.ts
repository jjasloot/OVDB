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
  RouteInstanceSearchResult
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
    MatPaginator,
    MatChipSet,
    MatChip
  ]
})
export class TrawellingComponent implements OnInit {
  connectionStatus: TrawellingConnectionStatus | null = null;
  unimportedTrips: TrawellingStatus[] = [];
  stats: TrawellingStats | null = null;
  loading = false;
  tripsLoading = false;
  statsLoading = false;
  
  // Pagination
  currentPage = 1;
  totalPages = 1;
  totalTrips = 0;
  tripsPerPage = 15;
  
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
  ) {}

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
          this.loadStats();
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
    
    this.apiService.getTrawellingUnimported(page).subscribe({
      next: (response: TrawellingStatusesResponse) => {
        this.unimportedTrips = response.data;
        this.currentPage = response.meta.current_page;
        this.tripsPerPage = response.meta.per_page;
        this.totalTrips = response.meta.total || 0;
        // Calculate total pages if not provided
        this.totalPages = response.meta.total ? Math.ceil(response.meta.total / response.meta.per_page) : page;
        this.tripsLoading = false;
      },
      error: (error) => {
        console.error('Error loading unimported trips:', error);
        this.showMessage('TRAEWELLING.ERROR_LOADING_TRIPS');
        this.tripsLoading = false;
      }
    });
  }

  loadStats(): void {
    this.statsLoading = true;
    this.apiService.getTrawellingStats().subscribe({
      next: (stats) => {
        this.stats = stats;
        this.statsLoading = false;
      },
      error: (error) => {
        console.error('Error loading Träwelling stats:', error);
        this.statsLoading = false;
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
          this.loadStats(); // Refresh stats
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
          this.loadStats(); // Refresh stats
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
    this.loadUnimportedTrips(this.currentPage);
    this.loadStats();
  }

  formatDuration(minutes?: number): string {
    if (!minutes) return '';
    const hours = Math.floor(minutes / 60);
    const mins = minutes % 60;
    return hours > 0 ? `${hours}h ${mins}m` : `${mins}m`;
  }

  formatDistance(meters?: number): string {
    if (!meters) return '';
    return meters > 1000 ? `${(meters / 1000).toFixed(1)} km` : `${meters} m`;
  }

  getCategoryName(category: number): string {
    const categories: { [key: number]: string } = {
      1: 'TRAEWELLING.CATEGORY_1',
      2: 'TRAEWELLING.CATEGORY_2', 
      3: 'TRAEWELLING.CATEGORY_3',
      4: 'TRAEWELLING.CATEGORY_4',
      5: 'TRAEWELLING.CATEGORY_5',
      6: 'TRAEWELLING.CATEGORY_6',
      7: 'TRAEWELLING.CATEGORY_7',
      8: 'TRAEWELLING.CATEGORY_8'
    };
    return categories[category] || '';
  }

  formatTime(dateString: string | null | undefined): string {
    if (!dateString) return '';
    return new Date(dateString).toLocaleTimeString('en-GB', { 
      hour: '2-digit', 
      minute: '2-digit' 
    });
  }

  formatDateTime(dateString: string | null | undefined): string {
    if (!dateString) return '';
    return new Date(dateString).toLocaleString('en-GB', { 
      day: '2-digit',
      month: '2-digit', 
      year: 'numeric',
      hour: '2-digit', 
      minute: '2-digit' 
    });
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