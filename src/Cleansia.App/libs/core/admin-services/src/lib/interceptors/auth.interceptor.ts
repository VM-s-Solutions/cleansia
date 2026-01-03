import { HttpInterceptorFn } from '@angular/common/http';
import { inject } from '@angular/core';
import { AdminAuthService } from '../services';

export const AuthInterceptorFn: HttpInterceptorFn = (req, next) => {
  const authService = inject(AdminAuthService);

  const token = authService.getToken();
  if (token) {
    const cloned = req.clone({
      headers: req.headers.set('Authorization', 'Bearer ' + token),
    });

    return next(cloned);
  }
  return next(req);
};
