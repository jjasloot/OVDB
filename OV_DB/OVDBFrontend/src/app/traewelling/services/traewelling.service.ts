import { Injectable, inject } from '@angular/core';
import { HttpClient, HttpParams } from '@angular/common/http';
import { Observable } from 'rxjs';
import { map, first } from 'rxjs/operators';
import { environment } from '../../../environments/environment';
import {
  TrawellingConnectionStatus,
  TrawellingTripsResponse,
  TrawellingTrip,
  LinkToRouteInstanceRequest,
  LinkToRouteInstanceResponse,
  TrawellingIgnoreRequest,
  TrawellingIgnoreResponse,
  RouteInstanceSearchResult,
  RouteSearchResult,
  TrawellingTripContext,
  TrawellingTransportCategory,
  RoutesListResponse
} from '../../models/traewelling.model';

@Injectable({
  providedIn: 'root'
})
export class TrawellingService {
  private http = inject(HttpClient);

  private readonly baseUrl = `${environment.backend}api/Traewelling`;
  private readonly routesUrl = `${environment.backend}api/routes`;

  // Connection management
  async getConnectionStatus(): Promise<TrawellingConnectionStatus> {
    return this.http.get<TrawellingConnectionStatus>(`${this.baseUrl}/status`)
      .pipe(first())
      .toPromise();
  }

  // Trip management
  async getUnimportedTrips(page: number = 1): Promise<TrawellingTripsResponse> {
    const params = new HttpParams().set('page', page.toString());
    return this.http.get<TrawellingTripsResponse>(`${this.baseUrl}/unimported`, { params })
      .pipe(first())
      .toPromise();
  }

  async ignoreTrip(tripId: number): Promise<TrawellingIgnoreResponse> {
    const request: TrawellingIgnoreRequest = { statusId: tripId };
    return this.http.post<TrawellingIgnoreResponse>(`${this.baseUrl}/ignore`, request)
      .pipe(first())
      .toPromise();
  }

  // Route Instance linking (existing functionality)
  async searchRouteInstances(trip: TrawellingTrip): Promise<RouteInstanceSearchResult[]> {
    const params = new HttpParams()
      .set('date', trip.transport.origin.departureScheduled.split('T')[0])
      .set('query', trip.transport.origin.name);
    
    return this.http.get<RouteInstanceSearchResult[]>(`${this.baseUrl}/route-instances`, { params })
      .pipe(first())
      .toPromise();
  }

  async linkToRouteInstance(tripId: number, routeInstanceId: number): Promise<LinkToRouteInstanceResponse> {
    const request: LinkToRouteInstanceRequest = { statusId: tripId, routeInstanceId };
    return this.http.post<LinkToRouteInstanceResponse>(`${this.baseUrl}/link`, request)
      .pipe(first())
      .toPromise();
  }

  // Route search for "Add to Existing Route" functionality
  async searchRoutes(filter: string): Promise<RouteSearchResult[]> {
    const params = new HttpParams()
      .set('start', '0')
      .set('count', '20')
      .set('filter', filter)
      .set('sortColumn','date')
      .set('descending',true);
    
    return this.http.get<RoutesListResponse>(this.routesUrl, { params })
      .pipe(
        map((routes) => routes.routes || []),
        first()
      )
      .toPromise();
  }

  // Trip context extraction for route creation workflows
  getTripContextForRouteCreation(trip: TrawellingTrip): TrawellingTripContext {
    return {
      tripId: trip.id,
      originName: trip.transport.origin.name,
      destinationName: trip.transport.destination.name,
      departureTime: trip.transport.origin.departureReal || trip.transport.origin.departureScheduled,
      arrivalTime: trip.transport.destination.arrivalReal || trip.transport.destination.arrivalScheduled,
      transportCategory: trip.transport.category,
      lineNumber: trip.transport.lineName,
      journeyNumber: trip.transport.journeyNumber,
      distance: trip.transport.distance,
      duration: trip.transport.duration,
      description: trip.body,
      tags: trip.tags,
      date: trip.createdAt
    };
  }

  // Material icon mapping for transport categories
  getTransportIcon(category: TrawellingTransportCategory): string {
    switch (category) {
      case TrawellingTransportCategory.BUS: 
        return 'directions_bus';
      case TrawellingTransportCategory.NATIONAL:
      case TrawellingTransportCategory.NATIONAL_EXPRESS:
      case TrawellingTransportCategory.REGIONAL:
      case TrawellingTransportCategory.REGIONAL_EXP:
        return 'train';
      case TrawellingTransportCategory.SUBWAY: 
        return 'subway';
      case TrawellingTransportCategory.TRAM: 
        return 'tram';
      case TrawellingTransportCategory.FERRY: 
        return 'directions_boat';
      case TrawellingTransportCategory.TAXI: 
        return 'local_taxi';
      default: 
        return 'directions_transit';
    }
  }

  // Color coding for transport categories
  getTransportColor(category: TrawellingTransportCategory): string {
    switch (category) {
      case TrawellingTransportCategory.BUS: 
        return '#2196F3'; // Blue
      case TrawellingTransportCategory.NATIONAL:
      case TrawellingTransportCategory.NATIONAL_EXPRESS:
      case TrawellingTransportCategory.REGIONAL:
      case TrawellingTransportCategory.REGIONAL_EXP:
        return '#FF9800'; // Orange
      case TrawellingTransportCategory.SUBWAY: 
        return '#9C27B0'; // Purple
      case TrawellingTransportCategory.TRAM: 
        return '#4CAF50'; // Green
      case TrawellingTransportCategory.FERRY: 
        return '#00BCD4'; // Cyan
      case TrawellingTransportCategory.TAXI: 
        return '#FFEB3B'; // Yellow
      default: 
        return '#757575'; // Gray
    }
  }

  // Format duration from minutes to readable format
  formatDuration(minutes: number): string {
    const hours = Math.floor(minutes / 60);
    const mins = Math.floor(minutes % 60);
    if (hours > 0) {
      return `${hours}h ${mins}m`;
    }
    return `${mins}m`;
  }
  formatDurationHours(hoursInput: number): string {
    const hours = Math.floor(hoursInput);
    const mins = Math.floor((hoursInput % 1) * 60);
    if (hours > 0) {
      return `${hours}h ${mins}m`;
    }
    return `${mins}m`;
  }

  // Format distance from meters to readable format
  formatDistance(meters: number): string {
    if (meters >= 1000) {
      return `${(meters / 1000).toFixed(1)} km`;
    }
    return `${meters} m`;
  }

  // Format time from ISO string to readable local time
  formatTime(isoString?: string): string {
    if (!isoString) return '';
    const date = new Date(isoString);
    return date.toLocaleTimeString('nl-NL', { 
      hour: '2-digit', 
      minute: '2-digit' 
    });
  }

  // Format date from ISO string to readable local date
  formatDate(isoString: string): string {
    const date = new Date(isoString);
    return date.toLocaleDateString('nl-NL', { 
      day: 'numeric', 
      month: 'short', 
      year: 'numeric' 
    });
  }
}