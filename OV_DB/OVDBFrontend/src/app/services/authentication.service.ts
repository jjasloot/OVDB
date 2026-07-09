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

  private readonly refreshBufferMs = 5 * 60 * 1000; // refresh this long before expiry
  private refreshInFlight: Promise<boolean> | null = null;

  constructor() {
    this.token = localStorage.getItem('OVDBToken');
    this.refreshToken = localStorage.getItem('OVDBRefreshToken');

    // Emit initial login state
    this.updateLoginState();

    // Pick up token changes made by other tabs so we never keep using a stale/rotated token,
    // and so a refresh performed by one tab is adopted by its siblings.
    window.addEventListener('storage', (event) => {
      if (event.key === 'OVDBToken' || event.key === 'OVDBRefreshToken') {
        this.syncFromStorage();
        this.updateLoginState();
        this.scheduleRefresh();
      }
    });

    // Don't force a refresh on every tab open — that races sibling tabs on the rotating refresh
    // token. Just schedule one shortly before expiry (refreshing immediately only if already due).
    this.scheduleRefresh();
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

    this.scheduleRefresh();

    // Emit login state change
    this.updateLoginState();
  }

  private syncFromStorage() {
    this.token = localStorage.getItem('OVDBToken');
    this.refreshToken = localStorage.getItem('OVDBRefreshToken');
  }

  private tokenValidForMs(): number {
    if (!this.token) {
      return -1;
    }
    const expirationDate = this.helper.getTokenExpirationDate(this.token);
    if (!expirationDate) {
      return -1;
    }
    return expirationDate.valueOf() - Date.now();
  }

  private scheduleRefresh() {
    if (this.refreshTrigger) {
      clearTimeout(this.refreshTrigger);
      this.refreshTrigger = null;
    }
    if (!this.token || !this.refreshToken) {
      return;
    }
    const remaining = this.tokenValidForMs();
    const delay = remaining - this.refreshBufferMs;
    if (delay <= 0) {
      this.refreshTheToken();
    } else {
      this.refreshTrigger = setTimeout(() => this.refreshTheToken(), delay);
    }
  }

  // Returns true if a valid token is in place afterwards. Coalesces concurrent callers within this
  // tab, and serializes across tabs via the Web Locks API so rotating refresh tokens aren't raced.
  refreshTheToken(): Promise<boolean> {
    if (this.refreshInFlight) {
      return this.refreshInFlight;
    }
    this.refreshInFlight = this.performRefresh().finally(() => {
      this.refreshInFlight = null;
    });
    return this.refreshInFlight;
  }

  private performRefresh(): Promise<boolean> {
    const run = async (): Promise<boolean> => {
      // Another tab may have refreshed while we waited for the lock — adopt its token instead.
      this.syncFromStorage();
      if (this.tokenValidForMs() > this.refreshBufferMs) {
        this.updateLoginState();
        return true;
      }
      if (!this.refreshToken) {
        return false;
      }
      try {
        const data: any = await new Promise((resolve, reject) => {
          this.httpClient.post(environment.backend + 'api/Authentication/refreshToken',
            { refreshToken: this.refreshToken }).subscribe({ next: resolve, error: reject });
        });
        this.HandleArrivalOfTokens(data);
        return true;
      } catch (error: any) {
        console.error('Token refresh failed:', error);
        if (error?.status === 401 || error?.status === 400) {
          this.logOut();
        }
        return false;
      }
    };

    const locks = (navigator as any)?.locks;
    if (locks?.request) {
      return locks.request('ovdb-token-refresh', run);
    }
    return run();
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
    if (!this.token) {
      return false;
    }
    return this.helper.decodeToken(this.token)?.admin === 'true';
  }

  getActiveSessions() {
    return this.httpClient.get<any[]>(environment.backend + 'api/Authentication/sessions');
  }

  revokeSession(sessionId: number) {
    return this.httpClient.post(environment.backend + `api/Authentication/revoke/${sessionId}`, {});
  }
}
