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
import { CleansiaAdminRoute } from '@cleansia/services';
import { Observable, catchError, switchMap, throwError } from 'rxjs';
import { AdminAuthService } from '../services/admin-auth.service';
import { AdminRefreshCoordinator } from './refresh-coordinator';

export const AdminErrorInterceptorFn: HttpInterceptorFn = (req, next) => {
  const authService = inject(AdminAuthService);
  const router = inject(Router);
  const coordinator = inject(AdminRefreshCoordinator);

  return next(req).pipe(
    catchError((error: HttpErrorResponse) => {
      if (error.status !== HttpStatusCode.Unauthorized || !req.url.includes('/api/')) {
        return throwError(() => error);
      }

      // Admin uses /api/AdminAuth/... — guard against infinite loops on refresh/login.
      if (req.url.includes('/api/AdminAuth/RefreshToken') || req.url.includes('/api/AdminAuth/Login')) {
        forceLogout(authService, router);
        return throwError(() => error);
      }

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
  authService: AdminAuthService,
  router: Router,
  coordinator: AdminRefreshCoordinator,
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
function withFreshCsrf(req: HttpRequest<unknown>, authService: AdminAuthService): HttpRequest<unknown> {
  const token = authService.getCsrfToken();
  return token && req.headers.has('X-CSRF-Token')
    ? req.clone({ headers: req.headers.set('X-CSRF-Token', token) })
    : req;
}

function forceLogout(authService: AdminAuthService, router: Router): void {
  if (authService.isLoggedIn() || authService.hasValidRefreshToken()) {
    authService.removeSession();
    router.navigate([`${CleansiaAdminRoute.LOGIN}`]);
  }
}
