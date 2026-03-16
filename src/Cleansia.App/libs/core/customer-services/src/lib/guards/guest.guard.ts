import { inject } from '@angular/core';
import { CanActivateFn, Router } from '@angular/router';
import { CustomerAuthService } from '../services';

export const customerGuestGuard: CanActivateFn = () => {
  const authService = inject(CustomerAuthService);
  const router = inject(Router);

  return authService.isLoggedIn() ? router.navigate(['orders']) : true;
};
