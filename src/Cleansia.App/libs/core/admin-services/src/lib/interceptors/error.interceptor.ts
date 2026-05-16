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
    // Wait for the in-flight refresh; rotated auth cookie carries the new
    // credentials, so the replay just sends the request as-is.
    return coordinator.waitForRefresh().pipe(switchMap(() => next(req)));
  }

  coordinator.begin();

  return authService.refreshSession().pipe(
    switchMap(() => {
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

function forceLogout(authService: AdminAuthService, router: Router): void {
  if (authService.isLoggedIn() || authService.hasValidRefreshToken()) {
    authService.removeSession();
    router.navigate([`${CleansiaAdminRoute.LOGIN}`]);
  }
}
