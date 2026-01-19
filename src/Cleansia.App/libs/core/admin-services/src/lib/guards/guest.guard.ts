import { inject } from '@angular/core';
import { CanActivateFn, Router } from '@angular/router';
import { CleansiaAdminRoute } from '@cleansia/services';
import { AdminAuthService } from '../services';

/**
 * Guard that prevents authenticated admin users from accessing guest-only pages
 * (login). Redirects logged-in admins to the employee management page.
 */
export const guestGuard: CanActivateFn = () => {
  const authService = inject(AdminAuthService);
  const router = inject(Router);

  return authService.isLoggedIn()
    ? router.navigate([CleansiaAdminRoute.EMPLOYEE_MANAGEMENT])
    : true;
};
