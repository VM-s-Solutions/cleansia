import { inject } from '@angular/core';
import { CanActivateFn, Router } from '@angular/router';
import { CleansiaPartnerRoute } from '../enums';
import { AuthService } from '../services';

export const authGuard: CanActivateFn = () => {
  const authService = inject(AuthService);
  const router = inject(Router);

  return authService.isLoggedIn()
    ? true
    : router.navigate([CleansiaPartnerRoute.LOGIN]);
};
