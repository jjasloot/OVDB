import {
  HttpContextToken,
  HttpErrorResponse,
  HttpEvent,
  HttpHandler,
  HttpInterceptor,
  HttpRequest,
} from "@angular/common/http";
import { Injectable, inject } from "@angular/core";
import { MatSnackBar } from "@angular/material/snack-bar";
import { TranslateService } from "@ngx-translate/core";
import { Observable, throwError } from "rxjs";
import { catchError } from "rxjs/operators";

/**
 * Set this token on a request's HttpContext to suppress the global error
 * snackbar for calls that present their own error UI.
 */
export const SKIP_ERROR_TOAST = new HttpContextToken<boolean>(() => false);

@Injectable()
export class HttpErrorInterceptor implements HttpInterceptor {
  private snackBar = inject(MatSnackBar);
  private translateService = inject(TranslateService);

  intercept(req: HttpRequest<any>, next: HttpHandler): Observable<HttpEvent<any>> {
    return next.handle(req).pipe(
      catchError((error: HttpErrorResponse) => {
        // 401s are handled by the AuthInterceptor (token refresh / login redirect).
        if (error.status !== 401 && !req.context.get(SKIP_ERROR_TOAST)) {
          // Importer endpoints return 502 when every Overpass mirror failed.
          const isOsmOutage =
            (error.status === 502 || error.status === 504) &&
            /\/api\/(station)?importer\//i.test(req.url);
          const key = isOsmOutage
            ? "ERRORS.OSM_UNAVAILABLE"
            : error.status === 0
              ? "ERRORS.NETWORK"
              : "ERRORS.GENERIC";
          this.snackBar.open(
            this.translateService.instant(key),
            this.translateService.instant("CLOSE"),
            { duration: 5000 }
          );
        }
        return throwError(() => error);
      })
    );
  }
}
