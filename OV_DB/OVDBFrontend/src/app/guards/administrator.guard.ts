import { Injectable, inject } from '@angular/core';
import { Route, UrlSegment, ActivatedRouteSnapshot, RouterStateSnapshot, UrlTree, Router } from '@angular/router';
import { AuthenticationService } from '../services/authentication.service';

@Injectable({
  providedIn: 'root'
})
export class AdministratorGuard  {
  authService = inject(AuthenticationService);
  router = inject(Router);


  canActivate(
    next: ActivatedRouteSnapshot,
    state: RouterStateSnapshot): Promise<boolean | UrlTree> {
    return this.check();
  }

  private async check(): Promise<boolean | UrlTree> {
    if (!(await this.authService.ensureLoggedIn()) || !this.authService.admin) {
      return this.router.parseUrl('/');
    }
    return true;
  }
}
