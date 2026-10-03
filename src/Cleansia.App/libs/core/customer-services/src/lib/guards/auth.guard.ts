import { isPlatformBrowser } from '@angular/common';
import { inject, PLATFORM_ID } from '@angular/core';
import { CanActivateFn, Router } from '@angular/router';
import { CleansiaCustomerRoute } from '@cleansia/services';
import { CustomerAuthService } from '../services';

export const customerAuthGuard: CanActivateFn = (_route, state) => {
  const platformId = inject(PLATFORM_ID);
  if (!isPlatformBrowser(platformId)) return true;

  const authService = inject(CustomerAuthService);
  const router = inject(Router);

  // The page asked for rides along so signing in lands on it: an order e-mail
  // links an account booking straight to its detail page.
  return authService.isLoggedIn()
    ? true
    : router.createUrlTree(['/' + CleansiaCustomerRoute.LOGIN], {
        queryParams: { returnUrl: state.url },
      });
};
