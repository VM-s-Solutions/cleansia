import {
  HttpErrorResponse,
  HttpInterceptorFn,
  HttpRequest,
  HttpHandlerFn,
  HttpEvent,
  HttpStatusCode,
} from '@angular/common/http';
import { inject } from '@angular/core';
import { Router } from '@angular/router';
import { BehaviorSubject, Observable, catchError, filter, switchMap, take, throwError } from 'rxjs';
import { CustomerAuthService } from '../services';

// Module-scoped single-flight state for refresh calls. If 10 requests 401 at
// once, only one actual refresh call fires; the other 9 wait for its result
// then retry with the new token.
let isRefreshing = false;
const refreshedToken$ = new BehaviorSubject<string | null>(null);

export const CustomerErrorInterceptorFn: HttpInterceptorFn = (req, next) => {
  const authService = inject(CustomerAuthService);
  const router = inject(Router);

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

      return handle401(req, next, authService, router);
    })
  );
};

function handle401(
  req: HttpRequest<unknown>,
  next: HttpHandlerFn,
  authService: CustomerAuthService,
  router: Router
): Observable<HttpEvent<unknown>> {
  if (isRefreshing) {
    // Another refresh is in flight; wait for its result then retry our request.
    return refreshedToken$.pipe(
      filter((token): token is string => token !== null),
      take(1),
      switchMap((token) => next(req.clone({ setHeaders: { Authorization: `Bearer ${token}` } })))
    );
  }

  isRefreshing = true;
  refreshedToken$.next(null);

  return authService.refreshSession().pipe(
    switchMap((newToken) => {
      isRefreshing = false;
      refreshedToken$.next(newToken);
      return next(req.clone({ setHeaders: { Authorization: `Bearer ${newToken}` } }));
    }),
    catchError((refreshError) => {
      isRefreshing = false;
      refreshedToken$.next(null);
      forceLogout(authService, router);
      return throwError(() => refreshError);
    })
  );
}

function forceLogout(authService: CustomerAuthService, router: Router): void {
  if (authService.isLoggedIn() || authService.hasValidRefreshToken()) {
    authService.removeSession();
    router.navigate(['login']);
  }
}
