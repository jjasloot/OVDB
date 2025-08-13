import { Component, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { Router } from '@angular/router';
import { MatSnackBar } from '@angular/material/snack-bar';
import { TranslateModule, TranslateService } from '@ngx-translate/core';
import { MatCard, MatCardContent, MatCardHeader, MatCardTitle } from '@angular/material/card';
import { MatButton } from '@angular/material/button';
import { MatIcon } from '@angular/material/icon';
import { MatProgressSpinner } from '@angular/material/progress-spinner';
import { MatPaginator, PageEvent } from '@angular/material/paginator';

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
    MatButton,
    MatIcon,
    MatProgressSpinner,
    MatPaginator
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
        this.totalPages = response.meta.current_page; // Note: API doesn't provide last_page, we'll handle this
        this.totalTrips = response.meta.total || 0;
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

  importTrip(trip: TrawellingTrip): void {
    const importRequest = {
      statusId: trip.id,
      createRoute: true
    };

    this.apiService.importTrawellingTrip(importRequest).subscribe({
      next: (response) => {
        if (response.success) {
          this.showMessage('TRAEWELLING.TRIP_IMPORTED');
          // Remove the imported trip from the list
          this.unimportedTrips = this.unimportedTrips.filter(t => t.id !== trip.id);
          this.loadStats(); // Refresh stats
        } else {
          this.showMessage('TRAEWELLING.ERROR_IMPORTING_TRIP');
        }
      },
      error: (error) => {
        console.error('Error importing trip:', error);
        this.showMessage('TRAEWELLING.ERROR_IMPORTING_TRIP');
      }
    });
  }

  useWizardForTrip(trip: TrawellingTrip): void {
    // Store trip data in session storage for the wizard
    sessionStorage.setItem('trawellingTrip', JSON.stringify(trip));
    this.router.navigate(['/admin/wizard']);
  }

  searchExistingRoutes(trip: TrawellingTrip): void {
    this.searchTrip = trip;
    this.searchingRouteInstances = true;
    this.existingRouteInstances = [];
    
    // Parse the trip departure date
    const tripDate = new Date(trip.departure);
    const dateString = tripDate.toISOString().split('T')[0]; // YYYY-MM-DD format
    
    // Create search query from origin and destination
    const searchQuery = `${trip.origin.name} ${trip.destination.name}`;
    
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

  linkToExistingRouteInstance(trip: TrawellingTrip, routeInstance: RouteInstanceSearchResult): void {
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

  processBacklog(): void {
    this.processingBacklog = true;
    this.apiService.processTrawellingBacklog().subscribe({
      next: (response) => {
        if (response.success) {
          this.showMessage('TRAEWELLING.BACKLOG_PROCESSED');
          this.loadUnimportedTrips(); // Refresh the list
          this.loadStats(); // Refresh stats
        } else {
          this.showMessage('TRAEWELLING.ERROR_PROCESSING_BACKLOG');
        }
        this.processingBacklog = false;
      },
      error: (error) => {
        console.error('Error processing backlog:', error);
        this.showMessage('TRAEWELLING.ERROR_PROCESSING_BACKLOG');
        this.processingBacklog = false;
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

  private showMessage(messageKey: string): void {
    this.translateService.get(messageKey).subscribe(message => {
      this.snackBar.open(message, '', { duration: 3000 });
    });
  }
}