import { inject } from '@angular/core';
import { CanActivateFn, Router } from '@angular/router';
import { CustomerAuthService } from '../services';

export const customerAuthGuard: CanActivateFn = () => {
  const authService = inject(CustomerAuthService);
  const router = inject(Router);

  return authService.isLoggedIn() ? true : router.navigate(['login']);
};
