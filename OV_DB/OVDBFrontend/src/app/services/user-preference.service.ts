import { Injectable, inject, signal } from '@angular/core';
import { AuthenticationService } from './authentication.service';
import { ApiService } from './api.service';
import { TranslationService } from './translation.service';
import { MapProviderService } from './map-provider.service';

@Injectable({
  providedIn: 'root'
})
export class UserPreferenceService {
  private authService = inject(AuthenticationService);
  private apiService = inject(ApiService);
  private translationService = inject(TranslationService);
  private mapProviderService = inject(MapProviderService);
  hasTraewelling = signal(false);

  applyUserLanguagePreference(): void {
    // Only fetch profile if we have a valid token
    if (this.authService.isLoggedIn) {
      this.apiService.getUserProfile().subscribe({
        next: (profile) => {
          // Apply preferred language if set and different from current
          if (profile.preferredLanguage && profile.preferredLanguage !== this.translationService.language) {
            this.translationService.language = profile.preferredLanguage as 'nl' | 'en';
          }
          this.hasTraewelling.set(profile.hasTraewelling ?? false);
          
          // Apply preferred map provider if set
          if (profile.preferredMapProvider) {
            this.mapProviderService.setProvider(profile.preferredMapProvider);
          }
        },
        error: (error) => {
          // Silently fail - if profile can't be fetched, just use browser language
          console.debug('Could not fetch user profile for language preference:', error);
        }
      });
    }
  }
}