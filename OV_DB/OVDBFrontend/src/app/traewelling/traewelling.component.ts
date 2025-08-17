import { Component, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { Router } from '@angular/router';
import { MatSnackBar } from '@angular/material/snack-bar';
import { MatDialog } from '@angular/material/dialog';
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
  TrawellingTripsResponse,
  TrawellingTrip,
  TrawellingStats,
  RouteInstanceSearchResult,
  TrawellingHafasTravelType
} from '../models/traewelling.model';
import { Route } from '../models/route.model';
import { RouteInstanceProperty } from '../models/routeInstanceProperty.model';
import { RouteSearchDialogComponent } from './route-search-dialog/route-search-dialog.component';
import { RouteInstancesEditComponent } from '../admin/route-instances-edit/route-instances-edit.component';

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
  unimportedTrips: TrawellingTrip[] = [];
  loading = false;
  tripsLoading = false;
  statsLoading = false;
  loadingMore = false;

  // Pagination
  currentPage = 1;
  hasMorePages = true;

  // Search and filters
  searchTerm = '';
  existingRouteInstances: RouteInstanceSearchResult[] = [];
  searchingRouteInstances = false;
  searchTrip: TrawellingTrip | null = null;

  constructor(
    public apiService: ApiService,
    public router: Router,
    private snackBar: MatSnackBar,
    private translateService: TranslateService,
    private dialog: MatDialog
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
          this.loadUnimportedTrips(this.currentPage);
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

  loadUnimportedTrips(page: number, showLoadingMore: boolean = false): void {
    if (showLoadingMore) {
      this.loadingMore = true;
    } else {
      this.tripsLoading = true;
    }

    this.currentPage = page;
    this.apiService.getTrawellingUnimported(this.currentPage).subscribe({
      next: (response: TrawellingTripsResponse) => {
        if (showLoadingMore) {
          this.unimportedTrips = this.unimportedTrips.concat(response.data);
        } else {
          this.unimportedTrips = response.data;
        }
        this.currentPage = response.meta.current_page;
        this.hasMorePages = response.hasMorePages;
        this.tripsLoading = false;
        this.loadingMore = false;
      },
      error: (error) => {
        console.error('Error loading unimported trips:', error);
        this.showMessage('TRAEWELLING.ERROR_LOADING_TRIPS');
        this.tripsLoading = false;
        this.loadingMore = false;
      }
    });

  }

  loadMoreTrips() {
    this.currentPage++;
    this.loadUnimportedTrips(this.currentPage, true);
  }


  onPageChange(event: PageEvent): void {
    this.loadUnimportedTrips(event.pageIndex + 1);
  }

  ignoreTrip(trip: TrawellingTrip): void {
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

  searchExistingRoutes(trip: TrawellingTrip): void {
    this.searchTrip = trip;
    this.searchingRouteInstances = true;
    this.existingRouteInstances = [];

    // Parse the trip departure date from transport origin departure
    const tripDate = trip.transport?.origin?.departureReal || trip.transport?.origin?.departureScheduled
      ? new Date(trip.transport.origin.departureReal || trip.transport.origin.departureScheduled!)
      : new Date(trip.createdAt);
    const dateString = tripDate.toISOString().split('T')[0]; // YYYY-MM-DD format

    // Create search query from origin and destination
    const searchQuery = trip.transport?.origin?.name && trip.transport?.destination?.name
      ? `${trip.transport.origin.name} ${trip.transport.destination.name}`
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

  isDelayed(planned: string | null | undefined, actual: string | null | undefined): boolean {
    if (!planned || !actual) return false;
    return new Date(actual).getTime() > new Date(planned).getTime();
  }

  getTransportCategoryTranslationKey(category: TrawellingHafasTravelType): string {
    return `TRAEWELLING.CATEGORY_${category.toString().toUpperCase()}`;
  }

  // New functionality for route creation and management
  addToExistingRoute(trip: TrawellingTrip): void {
    const dialogRef = this.dialog.open(RouteSearchDialogComponent, {
      data: { trip },
      width: '600px',
      maxHeight: '80vh'
    });

    dialogRef.afterClosed().subscribe(result => {
      if (result) {
        // User selected a route, now open route instance editor with pre-populated data
        this.openRouteInstanceEditor(result.route, result.trip);
      }
    });
  }

  private openRouteInstanceEditor(route: Route, trip: TrawellingTrip): void {
    // Create new RouteInstance with data from Träwelling trip
    const newInstance = {
      routeId: route.routeId,
      date: new Date(trip.createdAt).toISOString().split('T')[0], // Format as YYYY-MM-DD
      startTime: trip.transport?.origin?.departureReal || trip.transport?.origin?.departureScheduled,
      endTime: trip.transport?.destination?.arrivalReal || trip.transport?.destination?.arrivalScheduled,
      routeInstanceProperties: [],
      routeInstanceMaps: [],
      traewellingStatusId: trip.id // Link to Träwelling trip
    };

    // Add tags as properties
    if (trip.tags && trip.tags.length > 0) {
      trip.tags.forEach(tag => {
        newInstance.routeInstanceProperties.push({
          routeInstancePropertyId: 0, // Will be set by backend
          routeInstanceId: 0, // Will be set by backend
          key: tag.key,
          value: tag.value,
          bool: null
        } as RouteInstanceProperty);
      });
    }

    // Prepare Träwelling trip data for context display
    const tripData = {
      origin: trip.transport?.origin?.name,
      destination: trip.transport?.destination?.name,
      line: trip.transport?.lineName,
      number: trip.transport?.number,
      startTime: trip.transport?.origin?.departureReal || trip.transport?.origin?.departureScheduled,
      endTime: trip.transport?.destination?.arrivalReal || trip.transport?.destination?.arrivalScheduled,
      body: trip.body, // Comments
      tags: trip.tags,
      category: trip.transport?.category
    };

    // Open the route instance edit dialog
    const editDialogRef = this.dialog.open(RouteInstancesEditComponent, {
      data: { 
        instance: newInstance,
        new: true,
        trawellingTripData: tripData // Pass trip context for display
      },
      width: '80%',
      maxHeight: '80vh'
    });

    editDialogRef.afterClosed().subscribe(editedInstance => {
      if (editedInstance) {
        // Save the route instance
        this.apiService.updateRouteInstance(editedInstance).subscribe({
          next: () => {
            this.showMessage('TRAEWELLING.INSTANCE_CREATED');
            // Remove trip from unimported list
            this.unimportedTrips = this.unimportedTrips.filter(t => t.id !== trip.id);
          },
          error: (error) => {
            console.error('Error creating route instance:', error);
            this.showMessage('TRAEWELLING.ERROR_CREATING_INSTANCE');
          }
        });
      }
    });
  }

  createNewRoute(trip: TrawellingTrip): void {
    // Redirect to wizard with pre-populated data from Träwelling trip
    const tripData = {
      origin: trip.transport?.origin?.name,
      destination: trip.transport?.destination?.name,
      date: trip.createdAt,
      startTime: trip.transport?.origin?.departureReal || trip.transport?.origin?.departureScheduled,
      endTime: trip.transport?.destination?.arrivalReal || trip.transport?.destination?.arrivalScheduled,
      line: trip.transport?.lineName,
      number: trip.transport?.number,
      category: trip.transport?.category,
      tags: trip.tags,
      body: trip.body
    };
    
    // Store trip data in sessionStorage to pass to wizard
    sessionStorage.setItem('trawellingTripData', JSON.stringify(tripData));
    
    // Navigate to wizard - user can choose between wizard or GPX upload
    this.router.navigate(['/admin/wizard'], {
      queryParams: {
        fromTraewelling: 'true',
        tripId: trip.id
      }
    });
  }

  getCategoryIcon(category: TrawellingHafasTravelType): string {
    switch (category) {
      case TrawellingHafasTravelType.BUS: return 'directions_bus';
      case TrawellingHafasTravelType.NATIONAL:
      case TrawellingHafasTravelType.NATIONAL_EXPRESS:
      case TrawellingHafasTravelType.REGIONAL:
      case TrawellingHafasTravelType.REGIONAL_EXP:
        return 'train';
      case TrawellingHafasTravelType.SUBWAY: return 'subway';
      case TrawellingHafasTravelType.TRAM: return 'tram';
      case TrawellingHafasTravelType.FERRY: return 'directions_boat';
      case TrawellingHafasTravelType.TAXI: return 'local_taxi';
      default: return 'directions_transit';
    }
  }

  private showMessage(messageKey: string): void {
    this.translateService.get(messageKey).subscribe(message => {
      this.snackBar.open(message, '', { duration: 3000 });
    });
  }
}