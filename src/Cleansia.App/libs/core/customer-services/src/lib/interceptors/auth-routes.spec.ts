import { HttpClient } from '@angular/common/http';
import { of } from 'rxjs';
import {
  AuthClient,
  LoginCommand,
  RefreshTokenCommand,
} from '../client/customer-client';
import {
  CUSTOMER_LOGIN_PATH,
  CUSTOMER_REFRESH_TOKEN_PATH,
  isSessionIssuingRoute,
} from './auth-routes';

const API_BASE = 'https://api.cleansia.test';

describe('customer auth routes', () => {
  let request: jest.Mock;
  let client: AuthClient;

  beforeEach(() => {
    request = jest.fn(() => of());
    client = new AuthClient({ request } as unknown as HttpClient, API_BASE);
  });

  it('pins the refresh path to the route the generated client calls', () => {
    client.refreshToken(new RefreshTokenCommand()).subscribe();

    expect(request).toHaveBeenCalledWith(
      'post',
      `${API_BASE}${CUSTOMER_REFRESH_TOKEN_PATH}`,
      expect.anything()
    );
  });

  it('pins the login path to the route the generated client calls', () => {
    client.login(new LoginCommand()).subscribe();

    expect(request).toHaveBeenCalledWith(
      'post',
      `${API_BASE}${CUSTOMER_LOGIN_PATH}`,
      expect.anything()
    );
  });

  describe('isSessionIssuingRoute', () => {
    it.each([
      `${API_BASE}${CUSTOMER_REFRESH_TOKEN_PATH}`,
      `${API_BASE}${CUSTOMER_LOGIN_PATH}`,
      `${API_BASE}/api/auth/RefreshToken`,
      `${API_BASE}/api/auth/login`,
      '/API/AUTH/REFRESHTOKEN',
      `${API_BASE}${CUSTOMER_REFRESH_TOKEN_PATH}?trace=1`,
    ])('recognises %s regardless of case', (url) => {
      expect(isSessionIssuingRoute(url)).toBe(true);
    });

    it.each([
      `${API_BASE}/api/Auth/Logout`,
      `${API_BASE}/api/Auth/Register`,
      `${API_BASE}/api/Order/GetMyOrders`,
      `${API_BASE}/api/Auth/RefreshTokens`,
      '/assets/i18n/en.json',
    ])('leaves %s to the refresh flow', (url) => {
      expect(isSessionIssuingRoute(url)).toBe(false);
    });
  });
});
