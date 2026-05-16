import { AuthInterceptorFn } from './auth.interceptor';
import { AdminErrorInterceptorFn } from './error.interceptor';
import { LoadingInterceptorFn } from './loading.interceptor';

export * from './auth.interceptor';
export * from './error.interceptor';
export * from './loading.interceptor';

export const ADMIN_INTERCEPTORS_FN = [AuthInterceptorFn, AdminErrorInterceptorFn, LoadingInterceptorFn];
