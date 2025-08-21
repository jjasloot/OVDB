import { HttpInterceptor, HttpRequest, HttpHandler, HttpEvent } from '@angular/common/http';
import { Observable } from 'rxjs';
import { environment } from 'src/environments/environment';
import { AuthenticationService } from '../services/authentication.service';
import { Injectable, inject } from '@angular/core';


@Injectable()
export class AuthInterceptor implements HttpInterceptor {
    private authService = inject(AuthenticationService);



    intercept(req: HttpRequest<any>, next: HttpHandler): Observable<HttpEvent<any>> {
        if (req.url.startsWith(environment.backend)) {
            req = req.clone({
                setHeaders: {
                    Accept: 'application/json',
                    Authorization: `Bearer ${this.authService.token}`,
                },
            });
        }
        return next.handle(req);
    }
}
