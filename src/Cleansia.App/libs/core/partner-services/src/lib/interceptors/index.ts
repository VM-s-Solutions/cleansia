import { AuthInterceptorFn } from './auth.interceptor';
import { LoadingInterceptorFn } from './loading.interceptor';

export * from './auth.interceptor';
export * from './loading.interceptor';

export const PARTNER_INTERCEPTORS_FN = [
  AuthInterceptorFn,
  LoadingInterceptorFn,
];
