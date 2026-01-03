import { ContentDispositionInterceptorFn } from './content-disposition.interceptor';
import { HttpErrorInterceptorFn } from './http-error.interceptor';

export * from './content-disposition.interceptor';
export * from './http-error.interceptor';

export const COMMON_INTERCEPTORS_FN = [
  ContentDispositionInterceptorFn,
  HttpErrorInterceptorFn,
];
