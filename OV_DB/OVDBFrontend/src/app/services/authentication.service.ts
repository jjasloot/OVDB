import { Injectable, inject } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { environment } from 'src/environments/environment';
import { Router } from '@angular/router';
import { JwtHelperService } from '@auth0/angular-jwt';
import { RegistrationRequest } from '../models/registrationRequest.model';
import { tap } from 'rxjs/operators';
import { BehaviorSubject } from 'rxjs';

@Injectable({
  providedIn: 'root'
})
export class AuthenticationService {
  private httpClient = inject(HttpClient);
  private router = inject(Router);

  private isLoggedInSubject = new BehaviorSubject<boolean>(false);
  public isLoggedIn$ = this.isLoggedInSubject.asObservable();

  public token: string;
  public refreshToken: string | null;
  helper = new JwtHelperService();
  refreshTrigger: any;
  returnUrl: string;

  constructor() {
    this.token = localStorage.getItem('OVDBToken');
    this.refreshToken = localStorage.getItem('OVDBRefreshToken');
    
    // Emit initial login state
    this.updateLoginState();
    
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

    // Get token expiration date with null check
    const expirationDate = this.helper.getTokenExpirationDate(this.token);
    if (!expirationDate) {
      console.error('Invalid token - cannot schedule refresh');
      return;
    }

    const expiry = expirationDate.valueOf() - new Date().valueOf();
    const refreshBuffer = 5 * 60 * 1000; // 5 minutes in milliseconds
    this.refreshTrigger = setTimeout(() => this.refreshTheToken(), Math.max(expiry - refreshBuffer, 0));
    
    // Emit login state change
    this.updateLoginState();
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
          if (error.status === 401 || error.status === 400) {
            this.logOut();
          }
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
    
    // Emit login state change
    this.updateLoginState();
    
    this.router.navigate(['/']);
  }

  get isLoggedIn(): boolean {
    if (!this.token) {
      return false;
    }
    const expirationDate = this.helper.getTokenExpirationDate(this.token);
    if (!expirationDate) {
      return false;
    }
    return expirationDate > new Date();
  }

  private updateLoginState(): void {
    this.isLoggedInSubject.next(this.isLoggedIn);
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

  getActiveSessions() {
    return this.httpClient.get<any[]>(environment.backend + 'api/Authentication/sessions');
  }

  revokeSession(sessionId: number) {
    return this.httpClient.post(environment.backend + `api/Authentication/revoke/${sessionId}`, {});
  }
}
