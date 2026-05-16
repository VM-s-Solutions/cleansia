import { HttpInterceptorFn } from '@angular/common/http';
import { inject } from '@angular/core';
import { ADMINAPIBASEURL } from '../client/admin-client';
import { AdminAuthService } from '../services';

export const AuthInterceptorFn: HttpInterceptorFn = (req, next) => {
  // Scope to our own API. A bare `req.url.includes('/api/')` would also
  // attach credentials to any third-party URL containing `/api/` (Mapbox,
  // Sentry, analytics) and leak the session.
  const apiBaseUrl = inject(ADMINAPIBASEURL, { optional: true });
  if (!isOurApi(req.url, apiBaseUrl)) {
    return next(req);
  }

  const authService = inject(AdminAuthService);
  let headers = req.headers;

  if (isStateChanging(req.method)) {
    const csrfToken = authService.getCsrfToken();
    if (csrfToken) {
      headers = headers.set('X-CSRF-Token', csrfToken);
    }
  }

  // withCredentials carries the HttpOnly auth cookie + lets the browser
  // accept Set-Cookie responses. Required end-to-end for the cookie flow.
  const cloned = req.clone({ headers, withCredentials: true });
  return next(cloned);
};

function isStateChanging(method: string): boolean {
  const m = method.toUpperCase();
  return m === 'POST' || m === 'PUT' || m === 'PATCH' || m === 'DELETE';
}

function isOurApi(url: string, apiBaseUrl: string | null): boolean {
  if (!/^https?:\/\//i.test(url)) {
    return url.includes('/api/');
  }
  return apiBaseUrl ? url.startsWith(apiBaseUrl) : false;
}
