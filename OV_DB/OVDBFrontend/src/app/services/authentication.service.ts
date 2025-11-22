import { Injectable, inject } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { environment } from 'src/environments/environment';
import { Router } from '@angular/router';
import { JwtHelperService } from '@auth0/angular-jwt';
import { RegistrationRequest } from '../models/registrationRequest.model';
import { tap } from 'rxjs/operators';

@Injectable({
  providedIn: 'root'
})
export class AuthenticationService {
  private httpClient = inject(HttpClient);
  private router = inject(Router);



  public token: string;
  public refreshToken: string | null;
  helper = new JwtHelperService();
  refreshTrigger: any;
  returnUrl: string;
  
  constructor() {
    this.token = localStorage.getItem('OVDBToken');
    this.refreshToken = localStorage.getItem('OVDBRefreshToken');
    setTimeout(() => {
      if (this.token) {
        this.refreshTheToken();
      }
    }, 100);
  }

  login(email: string, password: string) {
    return this.httpClient.post(environment.backend + 'api/Authentication/login',
      { email, password }).pipe(tap((data: any) => {
        this.HandleArrivalOfTokens(data);

        if (this.returnUrl) {
          this.router.navigateByUrl(this.returnUrl);
        } else {
          this.router.navigate(['/']);
        }
      }));
  }
  
  registration(registration: RegistrationRequest) {
    return this.httpClient.post(environment.backend + 'api/Authentication/register', registration).pipe(tap(data => {
      this.HandleArrivalOfTokens(data);

      if (this.returnUrl) {
        this.router.navigateByUrl(this.returnUrl);
      } else {
        this.router.navigate(['/']);
      }
    }));
  }


  private HandleArrivalOfTokens(data: any) {
    localStorage.setItem('OVDBToken', data.token);
    this.token = data.token;
    
    // Store refresh token if provided
    if (data.refreshToken) {
      localStorage.setItem('OVDBRefreshToken', data.refreshToken);
      this.refreshToken = data.refreshToken;
    }
    
    const expiry = this.helper.getTokenExpirationDate(this.token).valueOf() - new Date().valueOf();
    this.refreshTrigger = setTimeout(() => this.refreshTheToken(), Math.max(expiry - (5 * 60 * 1000), 0));
  }

  refreshTheToken() {
    if (!this.refreshToken) {
      console.warn('No refresh token available');
      return;
    }
    
    this.httpClient.post(environment.backend + 'api/Authentication/refreshToken',
      { refreshToken: this.refreshToken }).subscribe({
        next: (data: any) => {
          this.HandleArrivalOfTokens(data);
        },
        error: (error) => {
          console.error('Token refresh failed:', error);
          // If refresh fails, log out the user
          this.logOut();
        }
      });
  }

  logOut() {
    if (this.refreshTrigger) {
      clearTimeout(this.refreshTrigger);
    }
    
    // Optionally notify backend to revoke the refresh token
    if (this.refreshToken) {
      this.httpClient.post(environment.backend + 'api/Authentication/logout',
        { refreshToken: this.refreshToken }).subscribe({
          error: (err) => console.error('Logout request failed:', err)
        });
    }
    
    localStorage.removeItem('OVDBToken');
    localStorage.removeItem('OVDBRefreshToken');
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
