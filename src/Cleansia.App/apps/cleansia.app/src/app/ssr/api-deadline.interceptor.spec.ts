/**
 * @jest-environment node
 * @jest-environment-options {"__Zone_disable_EventTarget": true}
 */
import {
  HttpClient,
  HttpErrorResponse,
  HttpHandlerFn,
  HttpRequest,
  HttpResponse,
  provideHttpClient,
  withFetch,
  withInterceptors,
} from '@angular/common/http';
import { PLATFORM_ID, RESPONSE_INIT } from '@angular/core';
import { TestBed, TestComponentRenderer } from '@angular/core/testing';
import { CUSTOMER_API_BASE_URL } from '@cleansia/customer-services';
import { createServer, type Server } from 'node:http';
import type { AddressInfo } from 'node:net';
import { performance } from 'node:perf_hooks';
import { catchError, firstValueFrom, of, throwError } from 'rxjs';
import { SsrApiDeadlineInterceptorFn } from './api-deadline.interceptor';

const API_BASE = 'https://api.cleansia.test/customer';
const GET_URL = `${API_BASE}/api/Market/GetOverview`;
const NODE_TEST_RENDERER = {
  provide: TestComponentRenderer,
  useValue: { removeAllRootElements: () => undefined },
};

describe('customer SSR API deadline', () => {
  let responseInit: ResponseInit;

  function configure(platform = 'server', base = API_BASE): void {
    responseInit = { headers: { 'x-existing': 'kept' } };
    TestBed.configureTestingModule({
      providers: [
        NODE_TEST_RENDERER,
        { provide: PLATFORM_ID, useValue: platform },
        { provide: CUSTOMER_API_BASE_URL, useValue: base },
        { provide: RESPONSE_INIT, useValue: responseInit },
      ],
    });
  }

  function forward(request: HttpRequest<unknown>): HttpRequest<unknown> {
    let forwarded: HttpRequest<unknown> | undefined;
    TestBed.runInInjectionContext(() =>
      SsrApiDeadlineInterceptorFn(request, (sent) => {
        forwarded = sent;
        return of(new HttpResponse({ body: [] }));
      }).subscribe(),
    );
    if (!forwarded) throw new Error('Request was not forwarded');
    return forwarded;
  }

  function fail(error: HttpErrorResponse): HttpErrorResponse {
    const next: HttpHandlerFn = () => throwError(() => error);
    let emitted: unknown;
    TestBed.runInInjectionContext(() =>
      SsrApiDeadlineInterceptorFn(new HttpRequest('GET', GET_URL), next)
        .subscribe({ error: (cause: unknown) => { emitted = cause; } }),
    );
    expect(emitted).toBe(error);
    return error;
  }

  beforeEach(() => configure());

  it('bounds server own-API GETs without changing credential or transfer-cache policy', () => {
    const request = new HttpRequest('GET', GET_URL, {
      withCredentials: true,
      transferCache: false,
    });
    const sent = forward(request);

    expect(sent.timeout).toBe(1500);
    expect(sent.withCredentials).toBe(request.withCredentials);
    expect(sent.transferCache).toBe(request.transferCache);
    expect(sent.headers).toBe(request.headers);
  });

  it('preserves a shorter explicit backend timeout', () => {
    const request = new HttpRequest('GET', GET_URL, { timeout: 40 });
    expect(forward(request).timeout).toBe(40);
  });

  it('caps a longer explicit backend timeout', () => {
    const request = new HttpRequest('GET', GET_URL, { timeout: 10000 });
    expect(forward(request).timeout).toBe(1500);
  });

  it('leaves browser GETs byte-identical', () => {
    TestBed.resetTestingModule();
    configure('browser');
    const request = new HttpRequest('GET', GET_URL);
    expect(forward(request)).toBe(request);
  });

  it.each(['POST', 'PUT', 'PATCH', 'DELETE', 'HEAD'])(
    'leaves server %s untouched',
    (method) => {
      const request = new HttpRequest(method, GET_URL, null);
      expect(forward(request)).toBe(request);
    },
  );

  it.each([
    'https://api.cleansia.test.evil.example/customer/api/Market/GetOverview',
    'https://api.cleansia.test/customer-two/api/Market/GetOverview',
    'https://api.cleansia.test/other/api/Market/GetOverview',
    'https://other.test/customer/api/Market/GetOverview',
    'http://api.cleansia.test/customer/api/Market/GetOverview',
    'https://api.cleansia.test:8443/customer/api/Market/GetOverview',
    'https://api.cleansia.test/customer/../other/api/Market/GetOverview',
    'https://api.cleansia.test/customer/assets/i18n/en.json',
    'https://other.test/?next=https://api.cleansia.test/customer/api/Market/GetOverview',
    '/assets/i18n/en.json',
  ])('leaves the foreign origin/path %s untouched', (url) => {
    const request = new HttpRequest('GET', url);
    expect(forward(request)).toBe(request);
  });

  it('accepts a base with a trailing slash', () => {
    TestBed.resetTestingModule();
    configure('server', `${API_BASE}/`);
    expect(forward(new HttpRequest('GET', GET_URL)).timeout).toBe(1500);
  });

  it('does not invent an API origin when the configured base is empty', () => {
    TestBed.resetTestingModule();
    configure('server', '');
    const request = new HttpRequest('GET', GET_URL);
    expect(forward(request)).toBe(request);
  });

  it('bounds relative own-API requests when the server has resolved an absolute origin', () => {
    TestBed.resetTestingModule();
    configure('server', 'https://api.cleansia.test');
    expect(forward(new HttpRequest('GET', '/api/Market/GetOverview')).timeout).toBe(1500);
  });

  it('keeps a valid empty result cache-admissible', () => {
    forward(new HttpRequest('GET', GET_URL));
    expect(new Headers(responseInit.headers).get('cache-control')).toBeNull();
  });

  it.each([0, 429, 500])('does not mark an immediate ordinary HTTP %s error as a timeout', (status) => {
    fail(new HttpErrorResponse({ status, error: new Error('ordinary failure') }));
    expect(new Headers(responseInit.headers).get('cache-control')).toBeNull();
  });

  it('marks only the current response no-store on a timeout error and rethrows it', () => {
    const error = new Error('signal timed out');
    error.name = 'TimeoutError';
    fail(new HttpErrorResponse({ error }));
    expect(new Headers(responseInit.headers).get('cache-control')).toBe('no-store');
    expect(new Headers(responseInit.headers).get('x-existing')).toBe('kept');

    TestBed.resetTestingModule();
    configure();
    forward(new HttpRequest('GET', GET_URL));
    expect(new Headers(responseInit.headers).get('cache-control')).toBeNull();
  });
});

// Node fetch errors cross Jest's VM realm; no-store is verified by the optimized-artifact smoke.
describe('customer SSR native FetchBackend cancellation', () => {
  let server: Server;

  afterEach(async () => {
    try {
      TestBed.resetTestingModule();
    } finally {
      server?.closeAllConnections();
      if (server?.listening) await new Promise<void>((resolve) => server.close(() => resolve()));
    }
  });

  it.each(['headers', 'body'] as const)(
    'aborts a stalled response %s and releases its real transport',
    async (stall) => {
      let accepted = false;
      let closed = false;
      let resolveClosed: () => void = () => undefined;
      const peerClosed = new Promise<void>((resolve) => { resolveClosed = resolve; });
      server = createServer((_req, res) => {
        accepted = true;
        res.once('close', () => { closed = true; resolveClosed(); });
        if (stall === 'body') {
          res.writeHead(200, { 'content-type': 'application/json' });
          res.write('[');
        }
      });
      await new Promise<void>((resolve) => server.listen(0, '127.0.0.1', resolve));
      const base = `http://127.0.0.1:${(server.address() as AddressInfo).port}`;
      TestBed.configureTestingModule({
        providers: [
          NODE_TEST_RENDERER,
          provideHttpClient(withFetch(), withInterceptors([SsrApiDeadlineInterceptorFn])),
          { provide: PLATFORM_ID, useValue: 'server' },
          { provide: CUSTOMER_API_BASE_URL, useValue: base },
        ],
      });
      const began = performance.now();
      const result: unknown = await firstValueFrom(
        TestBed.inject(HttpClient).get(`${base}/api/Market/GetOverview`, {
          responseType: 'text',
          ...(stall === 'body' ? { timeout: 100 } : {}),
        }).pipe(catchError((error: unknown) => of(error))),
      );
      const elapsed = performance.now() - began;
      expect(accepted).toBe(true);
      expect(result).toBeInstanceOf(HttpErrorResponse);
      const cause: unknown = result instanceof HttpErrorResponse ? result.error : undefined;
      expect(cause).toMatchObject({ name: 'TimeoutError' });
      expect(elapsed).toBeGreaterThanOrEqual(stall === 'body' ? 90 : 1400);
      expect(elapsed).toBeLessThan(stall === 'body' ? 1000 : 2500);
      await peerClosed;
      expect(closed).toBe(true);
    },
  );
});
