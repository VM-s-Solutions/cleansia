import { inject } from '@angular/core';
import { CanActivateFn, Router } from '@angular/router';
import { AdminAuthService } from '../services';

export const adminGuard: CanActivateFn = () => {
  const authService = inject(AdminAuthService);
  const router = inject(Router);

  // Check if user is logged in and has admin/editor role
  if (!authService.isLoggedIn()) {
    return router.navigate(['/login']);
  }

  if (!authService.isAdminOrEditor()) {
    // User is logged in but doesn't have admin privileges
    console.warn('Access denied: Admin privileges required');
    return router.navigate(['/unauthorized']);
  }

  return true;
};
