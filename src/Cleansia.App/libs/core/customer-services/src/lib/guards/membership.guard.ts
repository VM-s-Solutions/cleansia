import { isPlatformBrowser } from '@angular/common';
import { inject, PLATFORM_ID } from '@angular/core';
import { CanActivateFn, Router, UrlTree } from '@angular/router';
import { CleansiaCustomerRoute } from '@cleansia/services';
import { catchError, map, Observable, of } from 'rxjs';
import { CustomerClient } from '../client/customer-base-client';
import { CustomerAuthService } from '../services';

/**
 * Cleansia Plus, or the page that sells it.
 *
 * Some screens exist only for members — creating and editing a recurring schedule is the first —
 * and until now nothing stopped a signed-in non-member opening one. They got the whole wizard:
 * services, an address select, a calendar, a summary. The refusal arrived at SUBMIT, from the
 * server, as `recurring_booking.membership_required`, after the work was done.
 *
 * `CreateRecurringBooking` puts it plainly: "The client-side gates are UX, not the control." This
 * is that UX gate. It does not replace the server check and is not trusted to.
 *
 * ON THE SERVER, it passes. SSR has no session to read, and a guard that redirects during
 * pre-render ships the WRONG page to a member — the same shape `customerAuthGuard` uses, for the
 * same reason. The browser then runs it for real.
 *
 * ON A FAILED LOOKUP, it also passes. The alternative — bouncing to the sales page — tells a
 * paying member to buy what they already own the moment their connection wobbles, which reads as a
 * billing fault. A non-member let through by a network error is merely back where they were before
 * this guard existed, and the server still refuses them. Fail open: the cost is asymmetric and the
 * backend is the control.
 */
export const customerMembershipGuard: CanActivateFn = ():
  | boolean
  | UrlTree
  | Observable<boolean | UrlTree> => {
  const platformId = inject(PLATFORM_ID);
  if (!isPlatformBrowser(platformId)) {
    return true;
  }

  const router = inject(Router);
  const authService = inject(CustomerAuthService);

  // Signed out is a sign-in problem, not a membership one — say the right thing.
  if (!authService.isLoggedIn()) {
    return router.createUrlTree([CleansiaCustomerRoute.LOGIN]);
  }

  return inject(CustomerClient).membershipClient.getMine().pipe(
    map((membership) =>
      membership?.hasMembership === true
        ? true
        : router.createUrlTree(['/' + CleansiaCustomerRoute.PLUS]),
    ),
    catchError(() => of(true)),
  );
};
