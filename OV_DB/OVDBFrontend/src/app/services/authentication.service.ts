import { Injectable } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { environment } from 'src/environments/environment';
import { Router } from '@angular/router';
import { JwtHelperService } from '@auth0/angular-jwt';
import { RegistrationRequest } from '../models/registrationRequest.model';
import { tap } from 'rxjs/operators';
import { ApiService } from './api.service';
import { TranslationService } from './translation.service';

@Injectable({
  providedIn: 'root'
})
export class AuthenticationService {


  public token: string;
  refreshToken: any;
  helper = new JwtHelperService();
  refreshTrigger: any;
  returnUrl: string;
  constructor(private httpClient: HttpClient, private router: Router, private apiService: ApiService, private translationService: TranslationService) {
    this.token = localStorage.getItem('OVDBToken');
    setTimeout(() => {
      if (!!this.token) {
        this.refreshTheToken();
      }
      // Also apply language preference if already logged in
      this.applyUserLanguagePreference();
    }, 100);
  }

  login(email: string, password: string) {
    return this.httpClient.post(environment.backend + 'api/Authentication/login',
      { email, password }).pipe(tap((data: any) => {
        this.HandleArrivalOfTokens(data);

        if (!!this.returnUrl) {
          this.router.navigateByUrl(this.returnUrl);
        } else {
          this.router.navigate(['/']);
        }
      }));
  }
  registration(registration: RegistrationRequest) {
    return this.httpClient.post(environment.backend + 'api/Authentication/register', registration).pipe(tap(data => {
      this.HandleArrivalOfTokens(data);

      if (!!this.returnUrl) {
        this.router.navigateByUrl(this.returnUrl);
      } else {
        this.router.navigate(['/']);
      }
    }));
  }


  private HandleArrivalOfTokens(data: any) {
    localStorage.setItem('OVDBToken', data.token);
    this.token = data.token;
    const expiry = this.helper.getTokenExpirationDate(this.token).valueOf() - new Date().valueOf();
    this.refreshTrigger = setTimeout(() => this.refreshTheToken(), Math.max(expiry - (5 * 60 * 1000), 0));
    
    // Apply user's preferred language if set
    this.applyUserLanguagePreference();
  }

  private applyUserLanguagePreference() {
    // Only fetch profile if we have a valid token
    if (this.isLoggedIn) {
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

  refreshTheToken() {
    this.httpClient.post(environment.backend + 'api/Authentication/refreshToken',
      { refreshToken: this.refreshToken }).subscribe((data: any) => {
        this.HandleArrivalOfTokens(data);
      });
  }

  logOut() {
    if (this.refreshTrigger) {
      clearTimeout(this.refreshTrigger);
    }
    localStorage.removeItem('OVDBToken');
    this.refreshToken = null;
    this.token = null;
    this.router.navigate(['/']);
  }

  get isLoggedIn(): boolean {
    if (!this.token) {
      return false;
    }
    return (this.helper.getTokenExpirationDate(this.token) > new Date());
  }
  get autoUpdateRunning() {
    return !!this.refreshTrigger;
  }

  get email() {
    return this.helper.decodeToken(this.token)['http://schemas.xmlsoap.org/ws/2005/05/identity/claims/emailaddress'];
  }

  get admin() {
    return this.helper.decodeToken(this.token).admin === 'true';
  }
}
