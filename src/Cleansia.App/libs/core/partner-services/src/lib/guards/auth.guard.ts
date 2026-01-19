import { inject } from '@angular/core';
import { CanActivateFn, Router } from '@angular/router';
import { CleansiaPartnerRoute } from '@cleansia/services';
import { PartnerAuthService } from '../services';

export const authGuard: CanActivateFn = () => {
  const authService = inject(PartnerAuthService);
  const router = inject(Router);

  return authService.isLoggedIn()
    ? true
    : router.navigate([CleansiaPartnerRoute.LOGIN]);
};
