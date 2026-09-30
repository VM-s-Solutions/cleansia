import { inject } from '@angular/core';
import { ActivatedRouteSnapshot, CanActivateFn, Router } from '@angular/router';
import { CleansiaAdminRoute } from '@cleansia/services';
import { AdminAuthService } from '../services';

/**
 * An administrator whose password someone else chose is held on the profile's password change:
 * every other page lands there until the change succeeds, so nothing is reachable before it. The
 * hold is the UI's; the server reports the flag on every sign-in and refresh.
 */
export const adminGuard: CanActivateFn = (route: ActivatedRouteSnapshot) => {
  const authService = inject(AdminAuthService);
  const router = inject(Router);

  if (!authService.isLoggedIn()) {
    return router.createUrlTree([`/${CleansiaAdminRoute.LOGIN}`]);
  }

  if (!authService.isAdministrator()) {
    return router.createUrlTree([`/${CleansiaAdminRoute.UNAUTHORIZED}`]);
  }

  if (
    authService.passwordChangeRequired() &&
    route.routeConfig?.path !== CleansiaAdminRoute.PROFILE
  ) {
    return router.createUrlTree([`/${CleansiaAdminRoute.PROFILE}`]);
  }

  return true;
};
