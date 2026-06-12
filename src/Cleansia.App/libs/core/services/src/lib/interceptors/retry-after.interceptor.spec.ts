import { HttpClient, provideHttpClient, withInterceptors } from '@angular/common/http';
import {
  HttpTestingController,
  provideHttpClientTesting,
} from '@angular/common/http/testing';
import { PLATFORM_ID } from '@angular/core';
import { fakeAsync, TestBed, tick } from '@angular/core/testing';
import { TranslateService } from '@ngx-translate/core';

import { SnackbarService } from '../services';
import { HttpErrorInterceptorFn } from './http-error.interceptor';
import {
  retryAfterDelayMs,
  RetryAfterInterceptorFn,
} from './retry-after.interceptor';

describe('RetryAfterInterceptorFn (BSP-4c client back-off)', () => {
  const URL = '/api/things';
  const DEFAULT_BACKOFF_MS = 60_000;
  const JITTER_MAX_MS = 15_000;

  let showError: jest.Mock;
  let activeHttpMock: HttpTestingController | undefined;

  function setup(platform: 'browser' | 'server' = 'browser'): {
    http: HttpClient;
    httpMock: HttpTestingController;
  } {
    showError = jest.fn();
    TestBed.configureTestingModule({
      providers: [
        provideHttpClient(
          withInterceptors([HttpErrorInterceptorFn, RetryAfterInterceptorFn])
        ),
        provideHttpClientTesting(),
        { provide: PLATFORM_ID, useValue: platform },
        { provide: SnackbarService, useValue: { showError } },
        {
          provide: TranslateService,
          useValue: { instant: (key: string) => key },
        },
      ],
    });
    activeHttpMock = TestBed.inject(HttpTestingController);
    return {
      http: TestBed.inject(HttpClient),
      httpMock: activeHttpMock,
    };
  }

  function flush429(
    httpMock: HttpTestingController,
    headers?: Record<string, string>
  ): void {
    httpMock
      .expectOne(URL)
      .flush(null, { status: 429, statusText: 'Too Many Requests', headers });
  }

  afterEach(() => {
    activeHttpMock?.verify();
    activeHttpMock = undefined;
    jest.restoreAllMocks();
  });

  it('AC1: waits Retry-After seconds + jitter, then retries exactly once and delivers the retried response', fakeAsync(() => {
    jest.spyOn(Math, 'random').mockReturnValue(0.5);
    const expectedWaitMs = 2_000 + 0.5 * JITTER_MAX_MS;
    const { http, httpMock } = setup();

    let response: unknown;
    http.get(URL).subscribe((r) => (response = r));
    flush429(httpMock, { 'Retry-After': '2' });

    tick(expectedWaitMs - 1);
    httpMock.expectNone(URL);
    tick(1);
    httpMock.expectOne(URL).flush({ ok: true });

    expect(response).toEqual({ ok: true });
  }));

  it('AC1/AC8: a second 429 surfaces the error — max one retry, no third attempt', fakeAsync(() => {
    jest.spyOn(Math, 'random').mockReturnValue(0);
    const { http, httpMock } = setup();

    let surfaced: unknown;
    http.get(URL).subscribe({ error: (e: unknown) => (surfaced = e) });
    flush429(httpMock, { 'Retry-After': '1' });

    tick(1_000);
    flush429(httpMock, { 'Retry-After': '1' });

    tick(DEFAULT_BACKOFF_MS + JITTER_MAX_MS);
    httpMock.expectNone(URL);
    expect(surfaced).toBeTruthy();
  }));

  it('AC2: two rejections with the same Retry-After compute different waits (jitter desync)', () => {
    const first = retryAfterDelayMs('30', () => 0.2);
    const second = retryAfterDelayMs('30', () => 0.8);
    expect(first).not.toBe(second);
    expect(second - first).toBe(0.6 * JITTER_MAX_MS);
  });

  it('AC2/AC8: jitter is bounded to 0–15s on top of the base delay', () => {
    expect(retryAfterDelayMs('10', () => 0)).toBe(10_000);
    expect(retryAfterDelayMs('10', () => 0.999999)).toBeLessThan(
      10_000 + JITTER_MAX_MS
    );
  });

  it('AC3: missing Retry-After backs off the default 60s window + jitter, still one retry', fakeAsync(() => {
    jest.spyOn(Math, 'random').mockReturnValue(0.5);
    const expectedWaitMs = DEFAULT_BACKOFF_MS + 0.5 * JITTER_MAX_MS;
    const { http, httpMock } = setup();

    http.get(URL).subscribe({ error: () => undefined });
    flush429(httpMock);

    tick(expectedWaitMs - 1);
    httpMock.expectNone(URL);
    tick(1);
    httpMock.expectOne(URL).flush({ ok: true });
  }));

  it('AC3: malformed Retry-After falls back to the default window', () => {
    expect(retryAfterDelayMs('soon', () => 0)).toBe(DEFAULT_BACKOFF_MS);
    expect(retryAfterDelayMs('', () => 0)).toBe(DEFAULT_BACKOFF_MS);
    expect(retryAfterDelayMs('-5', () => 0)).toBe(DEFAULT_BACKOFF_MS);
    expect(retryAfterDelayMs(null, () => 0)).toBe(DEFAULT_BACKOFF_MS);
  });

  it('AC4: no snackbar on the first 429; exactly one after the retry is exhausted', fakeAsync(() => {
    jest.spyOn(Math, 'random').mockReturnValue(0);
    const { http, httpMock } = setup();

    http.get(URL).subscribe({ error: () => undefined });
    flush429(httpMock, { 'Retry-After': '1' });
    expect(showError).not.toHaveBeenCalled();

    tick(1_000);
    flush429(httpMock, { 'Retry-After': '1' });
    expect(showError).toHaveBeenCalledTimes(1);
  }));

  it('AC8: a Retry-After exceeding the default window is honored, not capped', () => {
    expect(retryAfterDelayMs('120', () => 0)).toBe(120_000);
  });

  it('non-429 errors pass through untouched — no retry, immediate error', fakeAsync(() => {
    const { http, httpMock } = setup();

    let surfaced: unknown;
    http.get(URL).subscribe({ error: (e: unknown) => (surfaced = e) });
    httpMock
      .expectOne(URL)
      .flush(null, { status: 500, statusText: 'Server Error' });

    expect(surfaced).toBeTruthy();
    tick(DEFAULT_BACKOFF_MS + JITTER_MAX_MS);
    httpMock.expectNone(URL);
  }));

  it('successful responses pass through with a single request', () => {
    const { http, httpMock } = setup();

    let response: unknown;
    http.get(URL).subscribe((r) => (response = r));
    httpMock.expectOne(URL).flush({ ok: true });

    expect(response).toEqual({ ok: true });
  });

  it('SSR: on the server platform a 429 surfaces immediately without retrying', fakeAsync(() => {
    const { http, httpMock } = setup('server');

    let surfaced: unknown;
    http.get(URL).subscribe({ error: (e: unknown) => (surfaced = e) });
    flush429(httpMock, { 'Retry-After': '1' });

    expect(surfaced).toBeTruthy();
    tick(DEFAULT_BACKOFF_MS + JITTER_MAX_MS);
    httpMock.expectNone(URL);
  }));
});
