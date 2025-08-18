import { Injectable } from '@angular/core';
import { HttpClient, HttpParams } from '@angular/common/http';
import { Observable } from 'rxjs';
import { 
  TrawellingConnectionStatus, 
  TrawellingTripsResponse, 
  TrawellingTrip,
  LinkToRouteInstanceRequest,
  LinkToRouteInstanceResponse,
  TrawellingIgnoreRequest,
  TrawellingIgnoreResponse,
  RouteInstanceSearchResult
} from '../../models/traewelling.model';

@Injectable({
  providedIn: 'root'
})
export class TrawellingService {

  constructor(private http: HttpClient) {}

  async getConnectionStatus(): Promise<TrawellingConnectionStatus> {
    return this.http.get<TrawellingConnectionStatus>('/api/traewelling/connection-status').toPromise();
  }

  async getUnimportedTrips(page: number = 1): Promise<TrawellingTripsResponse> {
    const params = new HttpParams().set('page', page.toString());
    return this.http.get<TrawellingTripsResponse>('/api/traewelling/unimported-trips', { params }).toPromise();
  }

  async searchRouteInstances(trip: TrawellingTrip): Promise<RouteInstanceSearchResult[]> {
    const params = new HttpParams()
      .set('startTime', trip.transport.origin.departureScheduled || '')
      .set('endTime', trip.transport.destination.arrivalScheduled || '')
      .set('originName', trip.transport.origin.name)
      .set('destinationName', trip.transport.destination.name);
    
    return this.http.get<RouteInstanceSearchResult[]>('/api/traewelling/search-route-instances', { params }).toPromise();
  }

  async linkToRouteInstance(request: LinkToRouteInstanceRequest): Promise<LinkToRouteInstanceResponse> {
    return this.http.post<LinkToRouteInstanceResponse>('/api/traewelling/link-to-route-instance', request).toPromise();
  }

  async ignoreTrip(request: TrawellingIgnoreRequest): Promise<TrawellingIgnoreResponse> {
    return this.http.post<TrawellingIgnoreResponse>('/api/traewelling/ignore', request).toPromise();
  }

  async searchRoutes(filter: string): Promise<any[]> {
    const params = new HttpParams()
      .set('start', '0')
      .set('count', '20')
      .set('filter', filter);
    
    return this.http.get<any[]>('/api/routes', { params }).toPromise();
  }

  getTripDataForRouteCreation(trip: TrawellingTrip) {
    return {
      originName: trip.transport.origin.name,
      destinationName: trip.transport.destination.name,
      departureTime: trip.transport.origin.departureScheduled,
      arrivalTime: trip.transport.destination.arrivalScheduled,
      transportType: trip.transport.category,
      lineNumber: trip.transport.lineName,
      journeyNumber: trip.transport.journeyNumber || trip.transport.number,
      distance: trip.transport.distance,
      duration: trip.transport.duration,
      description: trip.body,
      tags: trip.tags
    };
  }
}