import { InjectionToken } from '@angular/core';
import { LocalStorageKey } from '../enums/storage.enum';

export interface AuthCookieKeys {
  token: string;
  refreshToken: string;
  refreshTokenExp: string;
  role: string;
  // Double-submit CSRF token from the login/refresh response. Stored in
  // localStorage (JS-readable on purpose) and echoed back as X-CSRF-Token
  // on state-changing requests so the server can verify it matches the
  // session derived from the HttpOnly auth cookie.
  csrfToken: string;
}

/**
 * Per-app cookie / localStorage key names for auth state. Exists because admin
 * and partner both run on `localhost` in dev and cookies on `localhost` are
 * not port-scoped — without per-app key prefixes, signing in to one app
 * silently overwrites the other's session.
 *
 * Each app provides this token at bootstrap with its own prefix
 * (e.g. `partner_token`, `admin_token`). The default falls back to the
 * legacy unprefixed names so any consumer that forgets to provide it
 * still works (with the cross-app collision risk).
 */
export const AUTH_COOKIE_KEYS = new InjectionToken<AuthCookieKeys>(
  'AUTH_COOKIE_KEYS',
  {
    providedIn: 'root',
    factory: () => ({
      token: LocalStorageKey.TOKEN,
      refreshToken: LocalStorageKey.REFRESH_TOKEN,
      refreshTokenExp: LocalStorageKey.REFRESH_TOKEN_EXP,
      role: LocalStorageKey.ROLE,
      csrfToken: LocalStorageKey.CSRF_TOKEN,
    }),
  },
);
