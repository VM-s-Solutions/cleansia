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
import { Observable, defer, of, throwError } from 'rxjs';
import { CommonRoute } from '@cleansia/services';
import { PartnerAuthService } from '../services/partner-auth.service';
import { PARTNER_LOGIN_PATH, PARTNER_REFRESH_TOKEN_PATH } from './auth-routes';
import { PartnerErrorInterceptorFn } from './error.interceptor';
import { PartnerRefreshCoordinator } from './refresh-coordinator';

const API_BASE = 'https://api.cleansia.test';
const PROTECTED_PATH = '/api/Order/GetAvailableOrders';
const MUTATION_PATH = '/api/Order/TakeOrder';

function unauthorized(url: string): HttpErrorResponse {
  return new HttpErrorResponse({ status: HttpStatusCode.Unauthorized, url });
}

describe('PartnerErrorInterceptorFn', () => {
  let hasValidRefreshToken: jest.Mock<boolean, []>;
  let refreshSession: jest.Mock<Observable<boolean>, []>;
  let removeSession: jest.Mock<void, []>;
  let getCsrfToken: jest.Mock<string | null, []>;
  let navigate: jest.Mock<Promise<boolean>, [unknown[]]>;

  beforeEach(() => {
    hasValidRefreshToken = jest.fn<boolean, []>(() => true);
    refreshSession = jest.fn<Observable<boolean>, []>(() => of(true));
    removeSession = jest.fn<void, []>();
    getCsrfToken = jest.fn<string | null, []>(() => 'csrf-before-refresh');
    navigate = jest.fn<Promise<boolean>, [unknown[]]>(() => Promise.resolve(true));
    TestBed.configureTestingModule({
      providers: [
        {
          provide: PartnerAuthService,
          useValue: {
            isLoggedIn: () => true,
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

  function send(request: HttpRequest<unknown>, next: HttpHandlerFn): unknown[] {
    const errors: unknown[] = [];
    const events: HttpEvent<unknown>[] = [];
    TestBed.runInInjectionContext(() =>
      PartnerErrorInterceptorFn(request, next).subscribe({
        next: (event) => events.push(event),
        error: (error) => errors.push(error),
      })
    );
    return errors;
  }

  function mutation(url: string): HttpRequest<unknown> {
    return new HttpRequest('POST', url, null, {
      headers: new HttpHeaders({ 'X-CSRF-Token': 'csrf-before-refresh' }),
    });
  }

  describe('a 401 from a session-issuing route', () => {
    it.each([PARTNER_REFRESH_TOKEN_PATH, '/api/auth/RefreshToken', PARTNER_LOGIN_PATH])(
      'on %s forces logout and never asks for another refresh',
      (path) => {
        const url = `${API_BASE}${path}`;
        const { next, forwarded } = failOnceThenSucceed(url);

        const errors = send(new HttpRequest('POST', url, null), next);

        expect(refreshSession).not.toHaveBeenCalled();
        expect(forwarded).toHaveLength(1);
        expect(removeSession).toHaveBeenCalledTimes(1);
        expect(navigate).toHaveBeenCalledWith([`${CommonRoute.LOGIN}`]);
        expect((errors[0] as HttpErrorResponse).status).toBe(HttpStatusCode.Unauthorized);
        expect(TestBed.inject(PartnerRefreshCoordinator).isInFlight()).toBe(false);
      }
    );
  });

  describe('a mutation replayed after a refresh', () => {
    const url = `${API_BASE}${MUTATION_PATH}`;

    it('carries the CSRF token the refresh issued', () => {
      refreshSession.mockReturnValue(
        defer(() => {
          getCsrfToken.mockReturnValue('csrf-after-refresh');
          return of(true);
        })
      );
      const { next, forwarded } = failOnceThenSucceed(url);

      send(mutation(url), next);

      expect(forwarded).toHaveLength(2);
      expect(forwarded[0].headers.get('X-CSRF-Token')).toBe('csrf-before-refresh');
      expect(forwarded[1].headers.get('X-CSRF-Token')).toBe('csrf-after-refresh');
    });

    it('carries it too when it waited on a refresh another request started', () => {
      const coordinator = TestBed.inject(PartnerRefreshCoordinator);
      coordinator.begin();
      const { next, forwarded } = failOnceThenSucceed(url);

      send(mutation(url), next);
      getCsrfToken.mockReturnValue('csrf-after-refresh');
      coordinator.complete('csrf-after-refresh');

      expect(refreshSession).not.toHaveBeenCalled();
      expect(forwarded).toHaveLength(2);
      expect(forwarded[1].headers.get('X-CSRF-Token')).toBe('csrf-after-refresh');
    });

    it('never adds the header to a GET', () => {
      const getUrl = `${API_BASE}${PROTECTED_PATH}`;
      const { next, forwarded } = failOnceThenSucceed(getUrl);

      send(new HttpRequest('GET', getUrl), next);

      expect(forwarded).toHaveLength(2);
      expect(forwarded[1].headers.has('X-CSRF-Token')).toBe(false);
    });
  });
});
