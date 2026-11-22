import { Injectable, inject } from '@angular/core';
import { HttpClient, HttpContext } from '@angular/common/http';
import { environment } from 'src/environments/environment';
import { Router } from '@angular/router';
import { RegistrationRequest } from '../models/registrationRequest.model';
import { tap } from 'rxjs/operators';
import { BehaviorSubject } from 'rxjs';
import { SKIP_ERROR_TOAST } from '../guards/http-error.interceptor';
import { MatSnackBar } from '@angular/material/snack-bar';
import { TranslateService } from '@ngx-translate/core';

@Injectable({
  providedIn: 'root'
})
export class AuthenticationService {
  private httpClient = inject(HttpClient);
  private router = inject(Router);
  private snackBar = inject(MatSnackBar);
  private translateService = inject(TranslateService);

  private isLoggedInSubject = new BehaviorSubject<boolean>(false);
  public isLoggedIn$ = this.isLoggedInSubject.asObservable();

  public token: string | null;
  public refreshToken: string | null;
  refreshTrigger: any;
  returnUrl!: string;

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
      { email, password },
      { context: new HttpContext().set(SKIP_ERROR_TOAST, true) }).pipe(tap((data: any) => {
        this.HandleArrivalOfTokens(data);
        this.navigateAfterSignIn();
      }));
  }

  /** Back to the page that sent the user to log in, once; later sign-ins start from home. */
  private navigateAfterSignIn() {
    const target = this.returnUrl || '/';
    this.returnUrl = '';
    this.router.navigateByUrl(target);
  }

  registration(registration: RegistrationRequest) {
    return this.httpClient.post(environment.backend + 'api/Authentication/register', registration,
      { context: new HttpContext().set(SKIP_ERROR_TOAST, true) }).pipe(tap(data => {
      this.HandleArrivalOfTokens(data);
      this.navigateAfterSignIn();
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

  /** The JWT payload. Only read for expiry and the admin flag; the server verifies the token. */
  private static decodeToken(token: string): { exp?: unknown; admin?: unknown } | null {
    try {
      const payload = token.split('.')[1].replace(/-/g, '+').replace(/_/g, '/');
      return JSON.parse(new TextDecoder().decode(Uint8Array.from(atob(payload), c => c.charCodeAt(0))));
    } catch {
      return null;
    }
  }

  private static expirationDate(token: string): Date | null {
    const exp = AuthenticationService.decodeToken(token)?.exp;
    return typeof exp === 'number' ? new Date(exp * 1000) : null;
  }

  private tokenValidForMs(): number {
    if (!this.token) {
      return -1;
    }
    const expirationDate = AuthenticationService.expirationDate(this.token);
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
          this.sessionExpired();
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

  /** The refresh token is dead: say so, and offer the way back rather than a silent jump home. */
  private sessionExpired() {
    let route = this.router.routerState.snapshot.root;
    let needsLogin = false;
    while (route) {
      needsLogin ||= !!route.routeConfig?.canActivate?.length;
      route = route.firstChild!;
    }
    const url = this.router.url;
    // Public pages (a shared map) stay put; only pages that need a login send you to it.
    this.logOut(needsLogin ? '/login' : null);
    if (needsLogin) {
      this.returnUrl = url;
    }
    this.snackBar.open(
      this.translateService.instant('LOGIN.SESSION_EXPIRED'),
      this.translateService.instant('CLOSE'),
      { duration: 8000 }
    );
  }

  logOut(navigateTo: string | null = '/') {
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

    if (navigateTo) {
      this.router.navigateByUrl(navigateTo);
    }
  }

  /**
   * For route guards: an expired access token with a live refresh token is still a session — the
   * silent refresh just hasn't finished. Checking isLoggedIn alone bounced such users to /login.
   */
  async ensureLoggedIn(): Promise<boolean> {
    if (this.isLoggedIn) {
      return true;
    }
    return !!this.refreshToken && (await this.refreshTheToken()) && this.isLoggedIn;
  }

  get isLoggedIn(): boolean {
    if (!this.token) {
      return false;
    }
    const expirationDate = AuthenticationService.expirationDate(this.token);
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

  get admin() {
    if (!this.token) {
      return false;
    }
    return AuthenticationService.decodeToken(this.token)?.admin === 'true';
  }

  getActiveSessions() {
    return this.httpClient.get<any[]>(environment.backend + 'api/Authentication/sessions');
  }

  revokeSession(sessionId: number) {
    return this.httpClient.post(environment.backend + `api/Authentication/revoke/${sessionId}`, {});
  }
}
