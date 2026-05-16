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
import { CommonRoute } from '@cleansia/services';
import { Observable, catchError, switchMap, throwError } from 'rxjs';
import { PartnerAuthService } from '../services/partner-auth.service';
import { PartnerRefreshCoordinator } from './refresh-coordinator';

export const PartnerErrorInterceptorFn: HttpInterceptorFn = (req, next) => {
  const authService = inject(PartnerAuthService);
  const router = inject(Router);
  const coordinator = inject(PartnerRefreshCoordinator);

  return next(req).pipe(
    catchError((error: HttpErrorResponse) => {
      if (error.status !== HttpStatusCode.Unauthorized || !req.url.includes('/api/')) {
        return throwError(() => error);
      }

      // Infinite-loop guard: never try to refresh on the refresh/login endpoints.
      if (req.url.includes('/api/auth/RefreshToken') || req.url.includes('/api/auth/Login')) {
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
  authService: PartnerAuthService,
  router: Router,
  coordinator: PartnerRefreshCoordinator,
): Observable<HttpEvent<unknown>> {
  if (coordinator.isInFlight()) {
    // Wait for the in-flight refresh; the rotated auth cookie carries the
    // new credentials, so the replay just sends the request as-is.
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

function forceLogout(authService: PartnerAuthService, router: Router): void {
  if (authService.isLoggedIn() || authService.hasValidRefreshToken()) {
    authService.removeSession();
    router.navigate([`${CommonRoute.LOGIN}`]);
  }
}
