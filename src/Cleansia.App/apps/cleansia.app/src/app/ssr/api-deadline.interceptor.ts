import { isPlatformServer } from '@angular/common';
import { HttpErrorResponse, HttpInterceptorFn } from '@angular/common/http';
import { inject, PLATFORM_ID, RESPONSE_INIT } from '@angular/core';
import { CUSTOMER_API_BASE_URL } from '@cleansia/customer-services';
import { catchError, throwError } from 'rxjs';

const SSR_API_TIMEOUT_MS = 1500;

export const SsrApiDeadlineInterceptorFn: HttpInterceptorFn = (request, next) => {
  if (request.method !== 'GET' || !isPlatformServer(inject(PLATFORM_ID))) {
    return next(request);
  }

  const apiBaseUrl = inject(CUSTOMER_API_BASE_URL, { optional: true });
  if (!isOwnApi(request.url, apiBaseUrl)) return next(request);

  const responseInit = inject(RESPONSE_INIT, { optional: true });
  const timeout = Math.min(request.timeout ?? SSR_API_TIMEOUT_MS, SSR_API_TIMEOUT_MS);
  return next(request.clone({ timeout })).pipe(
    catchError((error: unknown) => {
      const cause: unknown = error instanceof HttpErrorResponse ? error.error : undefined;
      if (responseInit && cause instanceof Error && cause.name === 'TimeoutError') {
        const headers = new Headers(responseInit.headers);
        headers.set('cache-control', 'no-store');
        responseInit.headers = headers;
      }
      return throwError(() => error);
    }),
  );
};

function isOwnApi(url: string, apiBaseUrl: string | null): boolean {
  if (!apiBaseUrl) return false;
  try {
    const base = new URL(apiBaseUrl);
    const target = new URL(url, base);
    const apiPath = `${base.pathname.replace(/\/+$/, '')}/api/`;
    return target.origin === base.origin && target.pathname.startsWith(apiPath);
  } catch {
    return false;
  }
}
