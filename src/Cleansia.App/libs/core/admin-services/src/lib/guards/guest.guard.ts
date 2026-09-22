import { inject } from '@angular/core';
import { CanActivateFn, Router } from '@angular/router';
import { CleansiaAdminRoute } from '@cleansia/services';
import { AdminAuthService } from '../services';

/**
 * Keeps a signed-in administrator off the guest-only screens (login) by landing them on the home
 * route, which resolves to the first page their role can see.
 */
export const guestGuard: CanActivateFn = () => {
  const authService = inject(AdminAuthService);
  const router = inject(Router);

  return authService.isLoggedIn() ? router.createUrlTree([`/${CleansiaAdminRoute.HOME}`]) : true;
};
