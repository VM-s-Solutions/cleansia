import { inject } from '@angular/core';
import { CanActivateFn, Router } from '@angular/router';
import { CleansiaPartnerRoute } from '@cleansia/services';
import { AdminAuthService } from '../services';

export const authGuard: CanActivateFn = () => {
  const authService = inject(AdminAuthService);
  const router = inject(Router);

  return authService.isLoggedIn()
    ? true
    : router.navigate([CleansiaPartnerRoute.LOGIN]);
};
