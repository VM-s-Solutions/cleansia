import { HttpInterceptorFn } from '@angular/common/http';
import { inject } from '@angular/core';
import { CustomerAuthService } from '../services';

export const CustomerAuthInterceptorFn: HttpInterceptorFn = (req, next) => {
  const authService = inject(CustomerAuthService);

  const token = authService.getToken();
  if (token) {
    const cloned = req.clone({
      headers: req.headers.set('Authorization', 'Bearer ' + token),
    });

    return next(cloned);
  }
  return next(req);
};
