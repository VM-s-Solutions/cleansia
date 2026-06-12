import { ContentDispositionInterceptorFn } from './content-disposition.interceptor';
import { HttpErrorInterceptorFn } from './http-error.interceptor';
import { RetryAfterInterceptorFn } from './retry-after.interceptor';

export * from './content-disposition.interceptor';
export * from './http-error.interceptor';
export * from './retry-after.interceptor';

export const COMMON_INTERCEPTORS_FN = [
  ContentDispositionInterceptorFn,
  HttpErrorInterceptorFn,
  // After HttpErrorInterceptorFn on purpose: a 429 is retried here first, so
  // the error snackbar fires only once the single back-off retry is exhausted.
  RetryAfterInterceptorFn,
];
