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
  const stateChanging = isStateChanging(req.method);
  let headers = req.headers;

  // CSRF double-submit token. The auth cookie (HttpOnly) is the other half;
  // server verifies the two match on state-changing methods.
  if (stateChanging) {
    const csrfToken = authService.getCsrfToken();
    if (csrfToken) {
      headers = headers.set('X-CSRF-Token', csrfToken);
    }
  }

  // withCredentials carries the HttpOnly auth cookie and lets the browser accept Set-Cookie.
  // It goes only where the cookie is needed: every state-changing call (all auth routes —
  // login, refresh, logout, the OAuth exchanges — are POSTs), and any call made with a
  // session. Angular's transfer cache refuses a credentialed request, so an anonymous GET
  // must stay credential-less to be rendered once on the server and reused on bootstrap;
  // the same GET with a session keeps the cookie and is therefore never transferred.
  const withCredentials = stateChanging || authService.isLoggedIn();
  return next(req.clone({ headers, withCredentials }));
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
  // Absolute URL → must live under the configured API base. The boundary slash matters:
  // a bare prefix match would call `https://api.cleansia.test.evil.example/api/…` ours.
  const base = apiBaseUrl?.replace(/\/+$/, '');
  return base ? url.startsWith(`${base}/`) : false;
}
