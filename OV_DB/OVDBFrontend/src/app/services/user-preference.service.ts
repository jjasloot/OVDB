import { Injectable } from '@angular/core';
import { AuthenticationService } from './authentication.service';
import { ApiService } from './api.service';
import { TranslationService } from './translation.service';

@Injectable({
  providedIn: 'root'
})
export class UserPreferenceService {

  constructor(
    private authService: AuthenticationService,
    private apiService: ApiService,
    private translationService: TranslationService
  ) { }

  applyUserLanguagePreference(): void {
    // Only fetch profile if we have a valid token
    if (this.authService.isLoggedIn) {
      this.apiService.getUserProfile().subscribe({
        next: (profile) => {
          // Apply preferred language if set and different from current
          if (profile.preferredLanguage && profile.preferredLanguage !== this.translationService.language) {
            this.translationService.language = profile.preferredLanguage as 'nl' | 'en';
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