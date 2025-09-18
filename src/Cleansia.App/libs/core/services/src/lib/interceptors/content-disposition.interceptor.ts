import { HttpInterceptorFn } from '@angular/common/http';

export const ContentDispositionInterceptorFn: HttpInterceptorFn = (
  req,
  next
) => {
  const modifiedReq = req.clone({
    setHeaders: {
      'Content-Disposition': 'Cleansia',
    },
  });

  return next(modifiedReq);
};
