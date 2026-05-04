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
import {
  BehaviorSubject,
  Observable,
  catchError,
  filter,
  switchMap,
  take,
  throwError,
} from 'rxjs';
import { AdminAuthService } from '../services/admin-auth.service';

// Module-scoped single-flight refresh state.
let isRefreshing = false;
const refreshedToken$ = new BehaviorSubject<string | null>(null);

export const AdminErrorInterceptorFn: HttpInterceptorFn = (req, next) => {
  const authService = inject(AdminAuthService);
  const router = inject(Router);

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

      return handle401(req, next, authService, router);
    })
  );
};

function handle401(
  req: HttpRequest<unknown>,
  next: HttpHandlerFn,
  authService: AdminAuthService,
  router: Router
): Observable<HttpEvent<unknown>> {
  if (isRefreshing) {
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

function forceLogout(authService: AdminAuthService, router: Router): void {
  if (authService.isLoggedIn() || authService.hasValidRefreshToken()) {
    authService.removeSession();
    router.navigate([`${CleansiaAdminRoute.LOGIN}`]);
  }
}
