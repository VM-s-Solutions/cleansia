import { AuthInterceptorFn } from './auth.interceptor';
import { AdminErrorInterceptorFn } from './error.interceptor';

export * from './auth.interceptor';
export * from './error.interceptor';

export const ADMIN_INTERCEPTORS_FN = [AuthInterceptorFn, AdminErrorInterceptorFn];
