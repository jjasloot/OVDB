import { Injectable, inject } from '@angular/core';
import { Route, UrlSegment, ActivatedRouteSnapshot, RouterStateSnapshot, UrlTree, Router } from '@angular/router';
import { Observable } from 'rxjs';
import { AuthenticationService } from '../services/authentication.service';

@Injectable({
  providedIn: 'root'
})
export class AdministratorGuard  {
  authService = inject(AuthenticationService);
  router = inject(Router);


  canActivate(
    next: ActivatedRouteSnapshot,
    state: RouterStateSnapshot): Observable<boolean | UrlTree> | Promise<boolean | UrlTree> | boolean | UrlTree {
    try {
      if (!this.authService.isLoggedIn || !this.authService.admin) {
        return false;
      }
      return true;
    } catch {
      return false;
    }
  }
}
