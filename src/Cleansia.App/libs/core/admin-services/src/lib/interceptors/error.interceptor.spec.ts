import {
  HttpClient,
  HttpStatusCode,
  provideHttpClient,
  withInterceptors,
} from '@angular/common/http';
import {
  HttpTestingController,
  provideHttpClientTesting,
} from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { Router } from '@angular/router';
import { defer, of } from 'rxjs';

import { AdminAuthService } from '../services/admin-auth.service';
import { AdminErrorInterceptorFn } from './error.interceptor';
import { AdminRefreshCoordinator } from './refresh-coordinator';

/**
 * TC-ADMIN-TTL-3 — the CSRF-replay regression (T-0409 predecessor). The server derives the
 * double-submit CSRF key from the token's per-token `jti`, so every refresh rotates it. A 401→
 * refresh→replay must restamp `X-CSRF-Token` from the POST-refresh value; replaying the pre-refresh
 * header the auth interceptor stamped 403s on `csrf.header_mismatch`. This bites at the token-TTL
 * boundary in prod today and a 15-min admin TTL multiplies it ~96×.
 */
describe('AdminErrorInterceptorFn — CSRF restamp on 401 replay', () => {
  const URL = '/api/Things';
  let csrf: string;
  let httpMock: HttpTestingController;
  let http: HttpClient;
  let coordinator: {
    isInFlight: jest.Mock;
    begin: jest.Mock;
    complete: jest.Mock;
    fail: jest.Mock;
    waitForRefresh: jest.Mock;
  };

  function setup(inFlight: boolean): void {
    csrf = 'CSRF-OLD';
    coordinator = {
      isInFlight: jest.fn().mockReturnValue(inFlight),
      begin: jest.fn(),
      complete: jest.fn(),
      fail: jest.fn(),
      // The in-flight refresh rotates the token as it resolves, then notifies waiters.
      waitForRefresh: jest.fn().mockReturnValue(defer(() => {
        csrf = 'CSRF-NEW';
        return of('CSRF-NEW');
      })),
    };
    const authService: Partial<AdminAuthService> = {
      hasValidRefreshToken: () => true,
      isLoggedIn: () => true,
      getCsrfToken: () => csrf,
      // refreshSession rotates the CSRF as a side effect, exactly like setSession does in prod.
      refreshSession: () => defer(() => {
        csrf = 'CSRF-NEW';
        return of(true);
      }),
    };

    TestBed.configureTestingModule({
      providers: [
        provideHttpClient(withInterceptors([AdminErrorInterceptorFn])),
        provideHttpClientTesting(),
        { provide: AdminAuthService, useValue: authService },
        { provide: AdminRefreshCoordinator, useValue: coordinator },
        { provide: Router, useValue: { navigate: jest.fn() } },
      ],
    });
    http = TestBed.inject(HttpClient);
    httpMock = TestBed.inject(HttpTestingController);
  }

  afterEach(() => httpMock.verify());

  it('initiator branch: the replayed mutation carries the post-refresh CSRF token', () => {
    setup(false);

    http.post(URL, { x: 1 }, { headers: { 'X-CSRF-Token': 'CSRF-OLD' } }).subscribe();

    const first = httpMock.expectOne(URL);
    expect(first.request.headers.get('X-CSRF-Token')).toBe('CSRF-OLD');
    first.flush(null, { status: HttpStatusCode.Unauthorized, statusText: 'Unauthorized' });

    const replay = httpMock.expectOne(URL);
    expect(replay.request.headers.get('X-CSRF-Token')).toBe('CSRF-NEW');
    replay.flush({ ok: true });
  });

  it('waiter branch: a request queued behind an in-flight refresh also restamps CSRF', () => {
    setup(true);

    http.post(URL, { x: 2 }, { headers: { 'X-CSRF-Token': 'CSRF-OLD' } }).subscribe();

    const first = httpMock.expectOne(URL);
    first.flush(null, { status: HttpStatusCode.Unauthorized, statusText: 'Unauthorized' });

    const replay = httpMock.expectOne(URL);
    expect(replay.request.headers.get('X-CSRF-Token')).toBe('CSRF-NEW');
    replay.flush({ ok: true });
  });

  it('a GET without CSRF never gains a header on replay', () => {
    setup(false);

    http.get(URL).subscribe();

    const first = httpMock.expectOne(URL);
    first.flush(null, { status: HttpStatusCode.Unauthorized, statusText: 'Unauthorized' });

    const replay = httpMock.expectOne(URL);
    expect(replay.request.headers.has('X-CSRF-Token')).toBe(false);
    replay.flush({ ok: true });
  });
});
