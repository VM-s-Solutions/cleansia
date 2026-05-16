import { HttpInterceptorFn } from '@angular/common/http';
import { inject } from '@angular/core';
import { CUSTOMER_API_BASE_URL } from '../client/customer-base-client';
import { CustomerAuthService } from '../services';

export const CustomerAuthInterceptorFn: HttpInterceptorFn = (req, next) => {
  // Scope to our own API. A bare `req.url.includes('/api/')` would also
  // attach credentials to any third-party URL that happens to contain
  // `/api/` (Mapbox, Sentry, analytics SDKs) and leak the session.
  const apiBaseUrl = inject(CUSTOMER_API_BASE_URL, { optional: true });
  if (!isOurApi(req.url, apiBaseUrl)) {
    return next(req);
  }

  const authService = inject(CustomerAuthService);
  let headers = req.headers;

  // CSRF double-submit token. The auth cookie (HttpOnly) is the other half;
  // server verifies the two match on state-changing methods.
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
  // Relative URL (no scheme) → it's our own server.
  if (!/^https?:\/\//i.test(url)) {
    return url.includes('/api/');
  }
  // Absolute URL → must start with the configured API base.
  return apiBaseUrl ? url.startsWith(apiBaseUrl) : false;
}
