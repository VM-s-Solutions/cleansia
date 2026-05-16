import { AuthInterceptorFn } from './auth.interceptor';
import { PartnerErrorInterceptorFn } from './error.interceptor';
import { LoadingInterceptorFn } from './loading.interceptor';

export * from './auth.interceptor';
export * from './error.interceptor';
export * from './loading.interceptor';

export const PARTNER_INTERCEPTORS_FN = [
  AuthInterceptorFn,
  PartnerErrorInterceptorFn,
  LoadingInterceptorFn,
];
