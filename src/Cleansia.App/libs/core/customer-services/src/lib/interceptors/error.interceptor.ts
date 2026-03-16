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
      if (error.status === HttpStatusCode.Unauthorized) {
        // Clear any expired/invalid session and redirect to login
        authService.removeSession();
        router.navigate(['login']);
      }
      return throwError(() => error);
    })
  );
};
