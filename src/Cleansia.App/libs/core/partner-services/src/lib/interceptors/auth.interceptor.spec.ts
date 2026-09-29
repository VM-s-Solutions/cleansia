import { HttpHandlerFn, HttpRequest } from '@angular/common/http';
import { TestBed } from '@angular/core/testing';
import { of } from 'rxjs';
import { APIBASEURL } from '../client/partner-client';
import { PartnerAuthService } from '../services';
import { AuthInterceptorFn } from './auth.interceptor';

const API_BASE = 'https://api.cleansia.test';
const CSRF = 'csrf-123';

describe('AuthInterceptorFn (partner)', () => {
  let getCsrfToken: jest.Mock<string | null, []>;

  const configure = (apiBaseUrl: string): void => {
    TestBed.resetTestingModule();
    TestBed.configureTestingModule({
      providers: [
        { provide: APIBASEURL, useValue: apiBaseUrl },
        { provide: PartnerAuthService, useValue: { getCsrfToken } },
      ],
    });
  };

  beforeEach(() => {
    getCsrfToken = jest.fn<string | null, []>(() => CSRF);
    configure(API_BASE);
  });

  function send(method: string, url: string): HttpRequest<unknown> {
    const forwarded: HttpRequest<unknown>[] = [];
    const next: HttpHandlerFn = (req) => {
      forwarded.push(req);
      return of();
    };
    const body = method === 'GET' ? null : {};
    TestBed.runInInjectionContext(() =>
      AuthInterceptorFn(new HttpRequest(method, url, body), next).subscribe()
    );
    expect(forwarded).toHaveLength(1);
    return forwarded[0];
  }

  it.each([
    ['POST', `${API_BASE}/api/Order/Take`],
    ['POST', '/api/Order/Take'],
  ])('gives our own %s %s the cookie and the CSRF header', (method, url) => {
    const sent = send(method, url);

    expect(sent.withCredentials).toBe(true);
    expect(sent.headers.get('X-CSRF-Token')).toBe(CSRF);
  });

  it.each([
    ['POST', 'https://api.cleansia.test.evil.example/api/Order/Take'],
    ['GET', 'https://api.cleansia.testing/api/Employee/GetCurrent'],
    ['POST', 'https://sentry.io/api/1/envelope/'],
    ['GET', '/assets/i18n/en.json'],
  ])('gives %s %s neither the cookie nor the CSRF header', (method, url) => {
    const sent = send(method, url);

    expect(sent.withCredentials).toBe(false);
    expect(sent.headers.has('X-CSRF-Token')).toBe(false);
    expect(getCsrfToken).not.toHaveBeenCalled();
  });

  it('still recognises our API when the configured base carries a trailing slash', () => {
    configure(`${API_BASE}/`);

    const sent = send('POST', `${API_BASE}/api/Order/Take`);

    expect(sent.withCredentials).toBe(true);
    expect(sent.headers.get('X-CSRF-Token')).toBe(CSRF);
  });

  it('treats an absolute own-API URL as ours only when a base URL is configured', () => {
    configure('');

    const sent = send('GET', `${API_BASE}/api/Employee/GetCurrent`);

    expect(sent.withCredentials).toBe(false);
  });
});
