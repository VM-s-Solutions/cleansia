import {
  HttpErrorResponse,
  HttpInterceptorFn,
  HttpStatusCode,
} from '@angular/common/http';
import { inject } from '@angular/core';
import { Router } from '@angular/router';
import { catchError, throwError } from 'rxjs';
import { CustomerAuthService } from '../services';

export const CustomerErrorInterceptorFn: HttpInterceptorFn = (req, next) => {
  const authService = inject(CustomerAuthService);
  const router = inject(Router);

  return next(req).pipe(
    catchError((error: HttpErrorResponse) => {
      if (
        error.status === HttpStatusCode.Unauthorized &&
        req.url.includes('/api/') &&
        authService.isLoggedIn()
      ) {
        // Only redirect to login if this was an API call and the user was logged in
        // (meaning the token expired). Don't redirect for public pages.
        authService.removeSession();
        router.navigate(['login']);
      }
      return throwError(() => error);
    })
  );
};
