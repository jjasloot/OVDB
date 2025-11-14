import { Injectable, signal, inject } from '@angular/core';
import { MapProvider } from '../models/user-profile.model';
import { AuthenticationService } from './authentication.service';

@Injectable({
  providedIn: 'root'
})
export class MapProviderService {
  private authService = inject(AuthenticationService);
  
  // Signal to track the current map provider
  currentProvider = signal<MapProvider>('leaflet');
  
  // Set the map provider preference
  setProvider(provider: MapProvider): void {
    this.currentProvider.set(provider);
  }
  
  // Get the current map provider
  getProvider(): MapProvider {
    return this.currentProvider();
  }
  
  // Check if user is logged in to show toggle
  canToggleProvider(): boolean {
    return this.authService.isLoggedIn;
  }
}
