import { inject } from '@angular/core';
import { CanActivateFn, Router } from '@angular/router';
import { CleansiaPartnerRoute } from '@cleansia/services';
import { PartnerAuthService } from '../services';

/**
 * Keeps a signed-in cleaner off the guest-only screens (login, register, forgot-password,
 * confirm-email) by landing them on the dashboard.
 */
export const guestGuard: CanActivateFn = () => {
  const authService = inject(PartnerAuthService);
  const router = inject(Router);

  return authService.isLoggedIn() ? router.createUrlTree([`/${CleansiaPartnerRoute.DASHBOARD}`]) : true;
};
