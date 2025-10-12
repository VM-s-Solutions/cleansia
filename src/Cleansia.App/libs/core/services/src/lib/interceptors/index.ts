import { AuthInterceptorFn } from './auth.interceptor';
import { ContentDispositionInterceptorFn } from './content-disposition.interceptor';
import { HttpErrorInterceptorFn } from './http-error.interceptor';
import { LoadingInterceptorFn } from './loading.interceptor';

export * from './auth.interceptor';
export * from './content-disposition.interceptor';
export * from './http-error.interceptor';
export * from './loading.interceptor';

export const INTERCEPTORS_FN = [
  AuthInterceptorFn,
  ContentDispositionInterceptorFn,
  HttpErrorInterceptorFn,
  LoadingInterceptorFn,
];
