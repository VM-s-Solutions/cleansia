import { inject } from '@angular/core';
import { CanActivateFn, Router } from '@angular/router';
import { CleansiaPartnerRoute } from '@cleansia/services';
import { PartnerAuthService } from '../services';

/**
 * Guard that prevents authenticated users from accessing guest-only pages
 * (login, register, forgot-password, confirm-email).
 * Redirects logged-in users to the dashboard.
 */
export const guestGuard: CanActivateFn = () => {
  const authService = inject(PartnerAuthService);
  const router = inject(Router);

  return authService.isLoggedIn()
    ? router.navigate([CleansiaPartnerRoute.DASHBOARD])
    : true;
};
