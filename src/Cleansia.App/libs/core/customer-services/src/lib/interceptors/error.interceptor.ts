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

      // Don't try to refresh on the refresh/login endpoints themselves — infinite loop guard.
      if (req.url.includes('/api/auth/RefreshToken') || req.url.includes('/api/auth/Login')) {
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
    // Another refresh is in flight; wait for its result then replay our
    // request. The rotated auth cookie carries the new credentials — no
    // header swap needed.
    return coordinator.waitForRefresh().pipe(switchMap(() => next(req)));
  }

  coordinator.begin();

  return authService.refreshSession().pipe(
    switchMap(() => {
      // Pass the CSRF token forward to wake any other queued waiters.
      // (Value doesn't matter, just signals "refresh succeeded".)
      coordinator.complete(authService.getCsrfToken() ?? 'ok');
      return next(req);
    }),
    catchError((refreshError) => {
      coordinator.fail();
      forceLogout(authService, router);
      return throwError(() => refreshError);
    })
  );
}

function forceLogout(authService: CustomerAuthService, router: Router): void {
  if (authService.isLoggedIn() || authService.hasValidRefreshToken()) {
    authService.removeSession();
    router.navigate(['/' + CleansiaCustomerRoute.LOGIN]);
  }
}
