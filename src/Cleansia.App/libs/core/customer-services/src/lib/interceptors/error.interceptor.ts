import {
  HttpErrorResponse,
  HttpEvent,
  HttpHandlerFn,
  HttpInterceptorFn,
  HttpRequest,
  HttpStatusCode,
} from '@angular/common/http';
import { inject } from '@angular/core';
import { Router } from '@angular/router';
import { Observable, catchError, switchMap, throwError } from 'rxjs';
import { CleansiaCustomerRoute } from '@cleansia/services';
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

function forceLogout(authService: CustomerAuthService, router: Router): void {
  if (authService.isLoggedIn() || authService.hasValidRefreshToken()) {
    authService.removeSession();
    router.navigate(['/' + CleansiaCustomerRoute.LOGIN]);
  }
}
