import {
  HttpErrorResponse,
  HttpEvent,
  HttpHandlerFn,
  HttpHeaders,
  HttpRequest,
  HttpResponse,
  HttpStatusCode,
} from '@angular/common/http';
import { TestBed } from '@angular/core/testing';
import { Router } from '@angular/router';
import { Observable, Subject, defer, of, throwError } from 'rxjs';
import { CleansiaCustomerRoute } from '@cleansia/services';
import { CustomerAuthService } from '../services';
import {
  CUSTOMER_LOGIN_PATH,
  CUSTOMER_REFRESH_TOKEN_PATH,
} from './auth-routes';
import { CustomerErrorInterceptorFn } from './error.interceptor';
import { CustomerRefreshCoordinator } from './refresh-coordinator';

const API_BASE = 'https://api.cleansia.test';
const PROTECTED_PATH = '/api/Order/GetMyOrders';
const CSRF_CHECKED_MUTATION_PATH = '/api/Payment/CreateOrder';

const REFRESH_ROUTE_SPELLINGS = [
  CUSTOMER_REFRESH_TOKEN_PATH,
  '/api/auth/RefreshToken',
  '/api/auth/refreshtoken',
  '/api/AUTH/REFRESHTOKEN',
];

function unauthorized(url: string): HttpErrorResponse {
  return new HttpErrorResponse({ status: HttpStatusCode.Unauthorized, url });
}

describe('CustomerErrorInterceptorFn', () => {
  let isLoggedIn: jest.Mock<boolean, []>;
  let hasValidRefreshToken: jest.Mock<boolean, []>;
  let refreshSession: jest.Mock<Observable<boolean>, []>;
  let removeSession: jest.Mock<void, []>;
  let getCsrfToken: jest.Mock<string | null, []>;
  let navigate: jest.Mock<Promise<boolean>, [unknown[]]>;

  beforeEach(() => {
    isLoggedIn = jest.fn<boolean, []>(() => true);
    hasValidRefreshToken = jest.fn<boolean, []>(() => true);
    refreshSession = jest.fn<Observable<boolean>, []>(() => of(true));
    removeSession = jest.fn<void, []>();
    getCsrfToken = jest.fn<string | null, []>(() => 'csrf-123');
    navigate = jest.fn<Promise<boolean>, [unknown[]]>(() => Promise.resolve(true));
    TestBed.configureTestingModule({
      providers: [
        {
          provide: CustomerAuthService,
          useValue: {
            isLoggedIn,
            hasValidRefreshToken,
            refreshSession,
            removeSession,
            getCsrfToken,
          },
        },
        { provide: Router, useValue: { navigate } },
      ],
    });
  });

  interface Outcome {
    forwarded: HttpRequest<unknown>[];
    errors: unknown[];
    responses: HttpEvent<unknown>[];
  }

  function failOnceThenSucceed(url: string): {
    next: HttpHandlerFn;
    forwarded: HttpRequest<unknown>[];
  } {
    const forwarded: HttpRequest<unknown>[] = [];
    const next: HttpHandlerFn = (req) => {
      forwarded.push(req);
      return forwarded.length === 1
        ? throwError(() => unauthorized(url))
        : of(new HttpResponse({ status: HttpStatusCode.Ok, url }));
    };
    return { next, forwarded };
  }

  function send(
    url: string,
    next: HttpHandlerFn,
    method = 'GET'
  ): Omit<Outcome, 'forwarded'> {
    const errors: unknown[] = [];
    const responses: HttpEvent<unknown>[] = [];
    const request = new HttpRequest(method, url, null);
    TestBed.runInInjectionContext(() =>
      CustomerErrorInterceptorFn(request, next).subscribe({
        next: (event) => responses.push(event),
        error: (error) => errors.push(error),
      })
    );
    return { errors, responses };
  }

  function expectForcedLogout(): void {
    expect(removeSession).toHaveBeenCalledTimes(1);
    expect(navigate).toHaveBeenCalledTimes(1);
    expect(navigate).toHaveBeenCalledWith(['/' + CleansiaCustomerRoute.LOGIN]);
  }

  describe('a 401 from the refresh route itself', () => {
    it.each(REFRESH_ROUTE_SPELLINGS)(
      'on %s forces logout and never asks for another refresh',
      (path) => {
        const url = `${API_BASE}${path}`;
        const { next, forwarded } = failOnceThenSucceed(url);

        const { errors } = send(url, next, 'POST');

        expect(refreshSession).not.toHaveBeenCalled();
        expect(forwarded).toHaveLength(1);
        expectForcedLogout();
        expect(errors).toHaveLength(1);
        expect((errors[0] as HttpErrorResponse).status).toBe(HttpStatusCode.Unauthorized);
      }
    );

    it('clears a session that only the refresh-token expiry still vouches for', () => {
      isLoggedIn.mockReturnValue(false);
      hasValidRefreshToken.mockReturnValue(true);
      const url = `${API_BASE}/api/auth/RefreshToken`;
      const { next } = failOnceThenSucceed(url);

      send(url, next, 'POST');

      expect(refreshSession).not.toHaveBeenCalled();
      expectForcedLogout();
    });

    it('does not touch an already-absent session', () => {
      isLoggedIn.mockReturnValue(false);
      hasValidRefreshToken.mockReturnValue(false);
      const url = `${API_BASE}${CUSTOMER_REFRESH_TOKEN_PATH}`;
      const { next } = failOnceThenSucceed(url);

      const { errors } = send(url, next, 'POST');

      expect(refreshSession).not.toHaveBeenCalled();
      expect(removeSession).not.toHaveBeenCalled();
      expect(navigate).not.toHaveBeenCalled();
      expect(errors).toHaveLength(1);
    });

    it('leaves the coordinator idle for the next 401', () => {
      const url = `${API_BASE}${CUSTOMER_REFRESH_TOKEN_PATH}`;
      const { next } = failOnceThenSucceed(url);

      send(url, next, 'POST');

      expect(TestBed.inject(CustomerRefreshCoordinator).isInFlight()).toBe(false);
    });
  });

  describe('a 401 from the login route', () => {
    it.each([CUSTOMER_LOGIN_PATH, '/api/auth/login'])(
      'on %s forces logout without a refresh',
      (path) => {
        const url = `${API_BASE}${path}`;
        const { next } = failOnceThenSucceed(url);

        send(url, next, 'POST');

        expect(refreshSession).not.toHaveBeenCalled();
        expectForcedLogout();
      }
    );
  });

  describe('a 401 from any other API route', () => {
    it('triggers exactly one refresh and replays the request once', () => {
      const url = `${API_BASE}${PROTECTED_PATH}`;
      const { next, forwarded } = failOnceThenSucceed(url);

      const { errors, responses } = send(url, next);

      expect(refreshSession).toHaveBeenCalledTimes(1);
      expect(forwarded).toHaveLength(2);
      expect(forwarded[1].url).toBe(url);
      expect(responses).toHaveLength(1);
      expect(errors).toHaveLength(0);
      expect(removeSession).not.toHaveBeenCalled();
      expect(navigate).not.toHaveBeenCalled();
    });

    it('shares one refresh across concurrent 401s and replays each of them', () => {
      const refresh$ = new Subject<boolean>();
      refreshSession.mockReturnValue(refresh$.asObservable());
      const first = failOnceThenSucceed(`${API_BASE}${PROTECTED_PATH}`);
      const second = failOnceThenSucceed(`${API_BASE}/api/User/GetCurrent`);

      const firstOutcome = send(`${API_BASE}${PROTECTED_PATH}`, first.next);
      const secondOutcome = send(`${API_BASE}/api/User/GetCurrent`, second.next);
      refresh$.next(true);
      refresh$.complete();

      expect(refreshSession).toHaveBeenCalledTimes(1);
      expect(first.forwarded).toHaveLength(2);
      expect(second.forwarded).toHaveLength(2);
      expect(firstOutcome.responses).toHaveLength(1);
      expect(secondOutcome.responses).toHaveLength(1);
      expect(TestBed.inject(CustomerRefreshCoordinator).isInFlight()).toBe(false);
    });

    it('forces logout when the single refresh fails, without retrying it', () => {
      const refreshError = unauthorized(`${API_BASE}${CUSTOMER_REFRESH_TOKEN_PATH}`);
      refreshSession.mockReturnValue(throwError(() => refreshError));
      const url = `${API_BASE}${PROTECTED_PATH}`;
      const { next, forwarded } = failOnceThenSucceed(url);

      const { errors } = send(url, next);

      expect(refreshSession).toHaveBeenCalledTimes(1);
      expect(forwarded).toHaveLength(1);
      expectForcedLogout();
      expect(errors).toEqual([refreshError]);
      expect(TestBed.inject(CustomerRefreshCoordinator).isInFlight()).toBe(false);
    });

    it('forces logout straight away when no valid refresh token is held', () => {
      hasValidRefreshToken.mockReturnValue(false);
      const url = `${API_BASE}${PROTECTED_PATH}`;
      const { next, forwarded } = failOnceThenSucceed(url);

      const { errors } = send(url, next);

      expect(refreshSession).not.toHaveBeenCalled();
      expect(forwarded).toHaveLength(1);
      expectForcedLogout();
      expect(errors).toHaveLength(1);
    });
  });

  describe('a mutation replayed after a refresh', () => {
    const url = `${API_BASE}${CSRF_CHECKED_MUTATION_PATH}`;

    function sendMutation(next: HttpHandlerFn): void {
      const request = new HttpRequest('POST', url, null, {
        headers: new HttpHeaders({ 'X-CSRF-Token': 'csrf-before-refresh' }),
      });
      TestBed.runInInjectionContext(() =>
        CustomerErrorInterceptorFn(request, next).subscribe()
      );
    }

    beforeEach(() => getCsrfToken.mockReturnValue('csrf-before-refresh'));

    it('carries the CSRF token the refresh issued', () => {
      refreshSession.mockReturnValue(
        defer(() => {
          getCsrfToken.mockReturnValue('csrf-after-refresh');
          return of(true);
        })
      );
      const { next, forwarded } = failOnceThenSucceed(url);

      sendMutation(next);

      expect(forwarded).toHaveLength(2);
      expect(forwarded[0].headers.get('X-CSRF-Token')).toBe('csrf-before-refresh');
      expect(forwarded[1].headers.get('X-CSRF-Token')).toBe('csrf-after-refresh');
    });

    it('carries it too when it waited on a refresh another request started', () => {
      const coordinator = TestBed.inject(CustomerRefreshCoordinator);
      coordinator.begin();
      const { next, forwarded } = failOnceThenSucceed(url);

      sendMutation(next);
      getCsrfToken.mockReturnValue('csrf-after-refresh');
      coordinator.complete('csrf-after-refresh');

      expect(refreshSession).not.toHaveBeenCalled();
      expect(forwarded).toHaveLength(2);
      expect(forwarded[1].headers.get('X-CSRF-Token')).toBe('csrf-after-refresh');
    });

    it('never adds the header to a GET', () => {
      const getUrl = `${API_BASE}${PROTECTED_PATH}`;
      const { next, forwarded } = failOnceThenSucceed(getUrl);

      send(getUrl, next);

      expect(forwarded).toHaveLength(2);
      expect(forwarded[1].headers.has('X-CSRF-Token')).toBe(false);
    });
  });

  describe('anything the interceptor does not own', () => {
    it('passes a non-401 error through untouched', () => {
      const url = `${API_BASE}${PROTECTED_PATH}`;
      const serverError = new HttpErrorResponse({
        status: HttpStatusCode.InternalServerError,
        url,
      });
      const next: HttpHandlerFn = () => throwError(() => serverError);

      const { errors } = send(url, next);

      expect(errors).toEqual([serverError]);
      expect(refreshSession).not.toHaveBeenCalled();
      expect(removeSession).not.toHaveBeenCalled();
    });

    it('passes a 401 from outside our API through untouched', () => {
      const url = 'https://api.mapbox.com/search/v1';
      const next: HttpHandlerFn = () => throwError(() => unauthorized(url));

      const { errors } = send(url, next);

      expect(errors).toHaveLength(1);
      expect(refreshSession).not.toHaveBeenCalled();
      expect(removeSession).not.toHaveBeenCalled();
    });
  });
});
