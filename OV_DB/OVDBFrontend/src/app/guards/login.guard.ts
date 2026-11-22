import { Injectable, inject } from '@angular/core';
import { ActivatedRouteSnapshot, RouterStateSnapshot, UrlTree, Router } from '@angular/router';
import { AuthenticationService } from '../services/authentication.service';

@Injectable({
  providedIn: 'root'
})
export class LoginGuard  {
  authService = inject(AuthenticationService);
  router = inject(Router);

  async canActivate(next: ActivatedRouteSnapshot, state: RouterStateSnapshot): Promise<boolean | UrlTree> {
    if (await this.authService.ensureLoggedIn()) {
      return true;
    }
    // Only remembered when it is needed, so a later plain login doesn't jump to a stale page.
    this.authService.returnUrl = state.url;
    return this.router.createUrlTree(['/login'], { queryParams: { returnUrl: state.url } });
  }
}
