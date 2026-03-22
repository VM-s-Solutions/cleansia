import { isPlatformBrowser } from '@angular/common';
import { inject, PLATFORM_ID } from '@angular/core';
import { CanActivateFn, Router } from '@angular/router';
import { CustomerAuthService } from '../services';

export const customerAuthGuard: CanActivateFn = () => {
  const platformId = inject(PLATFORM_ID);
  if (!isPlatformBrowser(platformId)) return true;

  const authService = inject(CustomerAuthService);
  const router = inject(Router);

  return authService.isLoggedIn() ? true : router.navigate(['login']);
};
