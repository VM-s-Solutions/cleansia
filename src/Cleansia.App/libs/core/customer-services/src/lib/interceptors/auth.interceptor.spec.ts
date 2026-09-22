import { HttpHandlerFn, HttpRequest } from '@angular/common/http';
import { TestBed } from '@angular/core/testing';
import { of } from 'rxjs';
import { CUSTOMER_API_BASE_URL } from '../client/customer-base-client';
import { CustomerAuthService } from '../services';
import { CustomerAuthInterceptorFn } from './auth.interceptor';

const API_BASE = 'https://api.cleansia.test';
const CSRF = 'csrf-123';

// Angular's own predicate (transferCacheInterceptorFn → hasOutgoingCredentials): a request
// that satisfies it is skipped by the SSR transfer cache. Replicated here so the spec pins
// the interceptor against the rule the cache actually applies, not against our reading of it.
function hasOutgoingCredentials(req: HttpRequest<unknown>): boolean {
  return (
    req.withCredentials ||
    req.credentials === 'include' ||
    req.credentials === 'same-origin'
  );
}

function hasAuthHeaders(req: HttpRequest<unknown>): boolean {
  return (
    req.headers.has('authorization') ||
    req.headers.has('proxy-authorization') ||
    req.headers.has('cookie')
  );
}

function transferCacheAccepts(req: HttpRequest<unknown>): boolean {
  return (
    !hasOutgoingCredentials(req) &&
    !hasAuthHeaders(req) &&
    (req.method === 'GET' || req.method === 'HEAD')
  );
}

const ANONYMOUS_CATALOGUE_GETS = [
  '/api/Market/GetOverview',
  '/api/Membership/GetPlans?countryId=cz',
  '/api/Country/GetServiced',
  '/api/Country/GetPropertySizes?countryId=cz',
  '/api/Service/GetOverview?countryId=cz',
  '/api/Package/GetOverview?countryId=cz',
  '/api/Extra/GetOverview?countryId=cz',
];

const AUTH_ROUTES = [
  '/api/Auth/Login',
  '/api/Auth/Register',
  '/api/Auth/GoogleAuth',
  '/api/Auth/AppleAuth',
  '/api/Auth/ConfirmUserEmail',
  '/api/Auth/ResendConfirmationEmail',
  '/api/Auth/RefreshToken',
  '/api/Auth/Logout',
];

describe('CustomerAuthInterceptorFn', () => {
  let isLoggedIn: jest.Mock<boolean, []>;
  let getCsrfToken: jest.Mock<string | null, []>;

  beforeEach(() => {
    isLoggedIn = jest.fn<boolean, []>(() => false);
    getCsrfToken = jest.fn<string | null, []>(() => null);
    TestBed.configureTestingModule({
      providers: [
        { provide: CUSTOMER_API_BASE_URL, useValue: API_BASE },
        { provide: CustomerAuthService, useValue: { isLoggedIn, getCsrfToken } },
      ],
    });
  });

  function send(
    method: string,
    url: string,
    body: unknown = null
  ): HttpRequest<unknown> {
    const forwarded: HttpRequest<unknown>[] = [];
    const next: HttpHandlerFn = (req) => {
      forwarded.push(req);
      return of();
    };
    const request = new HttpRequest(method, url, body);
    TestBed.runInInjectionContext(() =>
      CustomerAuthInterceptorFn(request, next).subscribe()
    );
    expect(forwarded).toHaveLength(1);
    return forwarded[0];
  }

  describe('without a session', () => {
    it.each(ANONYMOUS_CATALOGUE_GETS)(
      'sends the anonymous GET %s credential-less, so the transfer cache accepts it',
      (path) => {
        const sent = send('GET', `${API_BASE}${path}`);

        expect(sent.withCredentials).toBe(false);
        expect(hasOutgoingCredentials(sent)).toBe(false);
        expect(transferCacheAccepts(sent)).toBe(true);
      }
    );

    it('sends a relative anonymous GET credential-less too', () => {
      const sent = send('GET', '/api/Market/GetOverview');

      expect(sent.withCredentials).toBe(false);
      expect(transferCacheAccepts(sent)).toBe(true);
    });

    it('never adds an auth or cookie header of its own to an anonymous GET', () => {
      const sent = send('GET', `${API_BASE}/api/Market/GetOverview`);

      expect(hasAuthHeaders(sent)).toBe(false);
      expect(sent.headers.keys()).toEqual([]);
    });

    it.each(AUTH_ROUTES)(
      'carries the cookie on the auth route %s because it is a POST, session or not',
      (path) => {
        const sent = send('POST', `${API_BASE}${path}`, {});

        expect(sent.withCredentials).toBe(true);
        expect(hasOutgoingCredentials(sent)).toBe(true);
      }
    );

    it.each(['POST', 'PUT', 'PATCH', 'DELETE'])(
      'carries the cookie on every state-changing %s',
      (method) => {
        const sent = send(method, `${API_BASE}/api/Order/Quote`, {});

        expect(sent.withCredentials).toBe(true);
      }
    );

    it('carries the cookie on a lower-case state-changing method', () => {
      const sent = send('post', `${API_BASE}/api/Order/Quote`, {});

      expect(sent.withCredentials).toBe(true);
    });

    it('adds no CSRF header when no token was ever issued', () => {
      const sent = send('POST', `${API_BASE}/api/Auth/Login`, {});

      expect(sent.headers.has('X-CSRF-Token')).toBe(false);
    });
  });

  describe('with a session', () => {
    beforeEach(() => {
      isLoggedIn.mockReturnValue(true);
      getCsrfToken.mockReturnValue(CSRF);
    });

    it.each(ANONYMOUS_CATALOGUE_GETS)(
      'keeps the cookie on GET %s, so the transfer cache refuses it',
      (path) => {
        const sent = send('GET', `${API_BASE}${path}`);

        expect(sent.withCredentials).toBe(true);
        expect(hasOutgoingCredentials(sent)).toBe(true);
        expect(transferCacheAccepts(sent)).toBe(false);
      }
    );

    it('keeps the cookie on a session-only GET', () => {
      const sent = send('GET', `${API_BASE}/api/Order/GetMyOrders`);

      expect(sent.withCredentials).toBe(true);
      expect(transferCacheAccepts(sent)).toBe(false);
    });

    it('keeps the cookie on HEAD, the other method the cache would take', () => {
      const sent = send('HEAD', `${API_BASE}/api/Market/GetOverview`);

      expect(sent.withCredentials).toBe(true);
      expect(transferCacheAccepts(sent)).toBe(false);
    });

    it('sends the CSRF double-submit header on a state-changing call', () => {
      const sent = send('POST', `${API_BASE}/api/Order/Quote`, {});

      expect(sent.headers.get('X-CSRF-Token')).toBe(CSRF);
      expect(sent.withCredentials).toBe(true);
    });

    it('sends no CSRF header on a GET', () => {
      const sent = send('GET', `${API_BASE}/api/Market/GetOverview`);

      expect(sent.headers.has('X-CSRF-Token')).toBe(false);
    });

    it('reads the session flag per request, not once at setup', () => {
      isLoggedIn.mockReturnValue(false);

      const sent = send('GET', `${API_BASE}/api/Market/GetOverview`);

      expect(sent.withCredentials).toBe(false);
    });
  });

  describe('outside our API', () => {
    it.each([
      ['GET', 'https://api.mapbox.com/api/search'],
      ['POST', 'https://sentry.io/api/1/envelope/'],
      ['GET', 'https://api.cleansia.test.evil.example/api/Market/GetOverview'],
      ['GET', '/assets/i18n/en.json'],
    ])(
      'gives %s %s neither the cookie nor the CSRF header, session or not',
      (method, url) => {
        isLoggedIn.mockReturnValue(true);
        getCsrfToken.mockReturnValue(CSRF);

        const sent = send(method, url, method === 'GET' ? null : {});

        expect(sent.withCredentials).toBe(false);
        expect(sent.headers.has('X-CSRF-Token')).toBe(false);
        expect(isLoggedIn).not.toHaveBeenCalled();
        expect(getCsrfToken).not.toHaveBeenCalled();
      }
    );

    it('still recognises our API when the configured base carries a trailing slash', () => {
      TestBed.resetTestingModule();
      TestBed.configureTestingModule({
        providers: [
          { provide: CUSTOMER_API_BASE_URL, useValue: `${API_BASE}/` },
          { provide: CustomerAuthService, useValue: { isLoggedIn, getCsrfToken } },
        ],
      });
      isLoggedIn.mockReturnValue(true);

      const sent = send('GET', `${API_BASE}/api/Market/GetOverview`);

      expect(sent.withCredentials).toBe(true);
    });

    it('treats an absolute own-API URL as ours only when a base URL is configured', () => {
      TestBed.resetTestingModule();
      TestBed.configureTestingModule({
        providers: [
          { provide: CUSTOMER_API_BASE_URL, useValue: '' },
          { provide: CustomerAuthService, useValue: { isLoggedIn, getCsrfToken } },
        ],
      });
      isLoggedIn.mockReturnValue(true);

      const sent = send('GET', `${API_BASE}/api/Market/GetOverview`);

      expect(sent.withCredentials).toBe(false);
    });
  });
});
