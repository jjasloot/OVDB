import { Injectable, inject, signal } from '@angular/core';
import { AuthenticationService } from './authentication.service';
import { ApiService } from './api.service';
import { TranslationService } from './translation.service';

@Injectable({
  providedIn: 'root'
})
export class UserPreferenceService {
  private authService = inject(AuthenticationService);
  private apiService = inject(ApiService);
  private translationService = inject(TranslationService);
  hasTraewelling = signal(false);
  enableTrainlogExport = signal(false);

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
          this.enableTrainlogExport.set(profile.enableTrainlogExport ?? false);
        },
        error: (error) => {
          // Silently fail - if profile can't be fetched, just use browser language
          console.debug('Could not fetch user profile for language preference:', error);
        }
      });
    }
  }

  constructor() {
    this.authService.isLoggedIn$.subscribe((loggedIn) => {
      if (loggedIn) {
        this.applyUserLanguagePreference();
      }
    });
  }
}