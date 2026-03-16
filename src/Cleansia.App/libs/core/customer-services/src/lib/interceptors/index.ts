import { CustomerAuthInterceptorFn } from './auth.interceptor';
import { CustomerErrorInterceptorFn } from './error.interceptor';
import { CustomerLoadingInterceptorFn } from './loading.interceptor';

export * from './auth.interceptor';
export * from './error.interceptor';
export * from './loading.interceptor';

export const CUSTOMER_INTERCEPTORS_FN = [
  CustomerAuthInterceptorFn,
  CustomerErrorInterceptorFn,
  CustomerLoadingInterceptorFn,
];
