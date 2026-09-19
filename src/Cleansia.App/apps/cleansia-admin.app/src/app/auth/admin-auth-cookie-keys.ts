import { AuthCookieKeys } from '@cleansia/services';

/**
 * The admin app's own key prefix: admin and partner share `localhost` in dev and cookies there
 * are not port-scoped, so an unprefixed session would overwrite the other app's.
 */
export const ADMIN_AUTH_COOKIE_KEYS: AuthCookieKeys = {
  token: 'admin_token',
  refreshToken: 'admin_refresh_token',
  refreshTokenExp: 'admin_refresh_token_exp',
  role: 'admin_role',
  csrfToken: 'admin_csrf',
  adminRole: 'admin_administrator_role',
  userId: 'admin_user_id',
};
