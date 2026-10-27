import {
  HttpErrorResponse,
  HttpEvent,
  HttpHandlerFn,
  HttpInterceptorFn,
  HttpRequest,
  HttpStatusCode,
} from '@angular/common/http';
import { inject } from '@angular/core';
import {
  ActivatedRouteSnapshot,
  NavigationCancel,
  NavigationEnd,
  NavigationError,
  NavigationSkipped,
  Router,
} from '@angular/router';
import { Observable, catchError, filter, of, switchMap, take, throwError } from 'rxjs';
import { CleansiaCustomerRoute } from '@cleansia/services';
import { customerAuthGuard } from '../guards/auth.guard';
import { CustomerAuthService } from '../services';
import { isSessionIssuingRoute } from './auth-routes';
import { CustomerRefreshCoordinator } from './refresh-coordinator';

export const CustomerErrorInterceptorFn: HttpInterceptorFn = (req, next) => {
  const authService = inject(CustomerAuthService);
  const router = inject(Router);
  const coordinator = inject(CustomerRefreshCoordinator);

  return next(req).pipe(
    catchError((error: HttpErrorResponse) => {
      // Only handle 401s on API calls.
      if (error.status !== HttpStatusCode.Unauthorized || !req.url.includes('/api/')) {
        return throwError(() => error);
      }

      // A 401 from the refresh or login call itself must not trigger another refresh.
      if (isSessionIssuingRoute(req.url)) {
        forceLogout(authService, router);
        return throwError(() => error);
      }

      // No valid refresh token → straight to logout.
      if (!authService.hasValidRefreshToken()) {
        forceLogout(authService, router);
        return throwError(() => error);
      }

      return handle401(req, next, authService, router, coordinator);
    })
  );
};

function handle401(
  req: HttpRequest<unknown>,
  next: HttpHandlerFn,
  authService: CustomerAuthService,
  router: Router,
  coordinator: CustomerRefreshCoordinator
): Observable<HttpEvent<unknown>> {
  if (coordinator.isInFlight()) {
    // Wait for the in-flight refresh, then replay with the POST-refresh CSRF token: the
    // server derives the double-submit key from the token's per-token `jti`, so every refresh
    // rotates the CSRF value. Replaying with the pre-refresh header the auth interceptor stamped
    // 403s on `csrf.header_mismatch`.
    return coordinator.waitForRefresh().pipe(switchMap(() => next(withFreshCsrf(req, authService))));
  }

  coordinator.begin();

  return authService.refreshSession().pipe(
    switchMap(() => {
      // Pass the CSRF token forward to wake any other queued waiters.
      // (Value doesn't matter, just signals "refresh succeeded".)
      coordinator.complete(authService.getCsrfToken() ?? 'ok');
      return next(withFreshCsrf(req, authService));
    }),
    catchError((refreshError) => {
      coordinator.fail();
      forceLogout(authService, router);
      return throwError(() => refreshError);
    })
  );
}

/**
 * Restamps `X-CSRF-Token` from the current (post-refresh) value before a replay. Only touches a
 * request that ALREADY carried the header (a mutation) — a GET without CSRF must not gain one.
 */
function withFreshCsrf(req: HttpRequest<unknown>, authService: CustomerAuthService): HttpRequest<unknown> {
  const token = authService.getCsrfToken();
  return token && req.headers.has('X-CSRF-Token')
    ? req.clone({ headers: req.headers.set('X-CSRF-Token', token) })
    : req;
}

/**
 * Signs the reader out, and sends them to sign in only from a page that needs an account, with that
 * page to return to, as the auth guard does; a public page (a guest's tracked booking, the booking
 * wizard, the login page itself) stays where it is.
 */
function forceLogout(authService: CustomerAuthService, router: Router): void {
  if (!authService.isLoggedIn() && !authService.hasValidRefreshToken()) {
    return;
  }
  authService.removeSession();
  pageSettled(router).subscribe(() => {
    if (needsAccount(router.routerState.snapshot.root)) {
      router.navigate(['/' + CleansiaCustomerRoute.LOGIN], { queryParams: { returnUrl: router.url } });
    }
  });
}

// A navigation under way has either passed the auth guard already, and lands on its page, or meets the
// guard now without a session and is turned away to sign in by the guard itself.
function pageSettled(router: Router): Observable<unknown> {
  if (!router.currentNavigation()) {
    return of(null);
  }
  return router.events.pipe(
    filter(
      (event) =>
        event instanceof NavigationEnd ||
        event instanceof NavigationSkipped ||
        event instanceof NavigationCancel ||
        event instanceof NavigationError
    ),
    take(1),
    filter((event) => event instanceof NavigationEnd || event instanceof NavigationSkipped)
  );
}

function needsAccount(root: ActivatedRouteSnapshot): boolean {
  for (let route: ActivatedRouteSnapshot | null = root; route; route = route.firstChild) {
    if (route.routeConfig?.canActivate?.includes(customerAuthGuard)) {
      return true;
    }
  }
  return false;
}
