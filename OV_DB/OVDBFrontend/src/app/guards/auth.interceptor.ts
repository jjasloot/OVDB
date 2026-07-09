import { HttpInterceptor, HttpRequest, HttpHandler, HttpEvent, HttpErrorResponse } from '@angular/common/http';
import { Observable, from, throwError } from 'rxjs';
import { catchError, switchMap } from 'rxjs/operators';
import { environment } from 'src/environments/environment';
import { AuthenticationService } from '../services/authentication.service';
import { Injectable, inject } from '@angular/core';


@Injectable()
export class AuthInterceptor implements HttpInterceptor {
    private authService = inject(AuthenticationService);

    private addToken(req: HttpRequest<any>): HttpRequest<any> {
        return req.clone({
            setHeaders: {
                Accept: 'application/json',
                Authorization: `Bearer ${this.authService.token}`,
            },
        });
    }

    intercept(req: HttpRequest<any>, next: HttpHandler): Observable<HttpEvent<any>> {
        const isBackend = req.url.startsWith(environment.backend);
        // Never attach/refresh tokens for the auth endpoints themselves (login/refresh/logout).
        const isAuthEndpoint = req.url.includes('api/Authentication/');

        if (isBackend) {
            req = this.addToken(req);
        }

        return next.handle(req).pipe(
            catchError((error: HttpErrorResponse) => {
                if (error.status === 401 && isBackend && !isAuthEndpoint) {
                    // Access token likely expired — refresh once and retry the original request.
                    return from(this.authService.refreshTheToken()).pipe(
                        switchMap((refreshed) => {
                            if (refreshed) {
                                return next.handle(this.addToken(req));
                            }
                            return throwError(() => error);
                        })
                    );
                }
                return throwError(() => error);
            })
        );
    }
}
