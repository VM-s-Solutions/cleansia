import { inject } from '@angular/core';
import { CanActivateFn, Router } from '@angular/router';
import { CleansiaAdminRoute } from '@cleansia/services';
import { AdminAuthService } from '../services';

export const adminGuard: CanActivateFn = () => {
  const authService = inject(AdminAuthService);
  const router = inject(Router);

  if (!authService.isLoggedIn()) {
    return router.createUrlTree([`/${CleansiaAdminRoute.LOGIN}`]);
  }

  if (!authService.isAdministrator()) {
    return router.createUrlTree([`/${CleansiaAdminRoute.UNAUTHORIZED}`]);
  }

  return true;
};
