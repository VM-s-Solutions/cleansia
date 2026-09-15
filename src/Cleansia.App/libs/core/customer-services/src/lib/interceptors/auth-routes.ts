// The generated client builds these paths inline and exports nothing to import them from;
// auth-routes.spec.ts pins both constants against the URL the client actually requests.
export const CUSTOMER_REFRESH_TOKEN_PATH = '/api/Auth/RefreshToken';
export const CUSTOMER_LOGIN_PATH = '/api/Auth/Login';

const SESSION_ISSUING_ROUTE = new RegExp(
  `(?:${[CUSTOMER_REFRESH_TOKEN_PATH, CUSTOMER_LOGIN_PATH].join('|')})(?=$|[?#/])`,
  'i'
);

export function isSessionIssuingRoute(url: string): boolean {
  return SESSION_ISSUING_ROUTE.test(url);
}
