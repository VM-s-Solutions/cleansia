import {
  HttpClient,
  HttpContext,
  provideHttpClient,
  withInterceptors,
} from '@angular/common/http';
import {
  HttpTestingController,
  provideHttpClientTesting,
} from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { TranslateService } from '@ngx-translate/core';

import { SnackbarService } from '../services';
import { SUPPRESS_ERROR_TOAST } from './error-toast-suppression';
import { HttpErrorInterceptorFn } from './http-error.interceptor';

describe('HttpErrorInterceptorFn (EP-1/AC4 error-key resolution + fallback)', () => {
  const URL = '/api/order/cancel';
  const FALLBACK_MESSAGE = 'Something went wrong. Please try again.';
  const UNAUTHORIZED_MESSAGE = 'Not authorized';

  const KNOWN_TRANSLATIONS: Record<string, string> = {
    'api.common.error_occurred': FALLBACK_MESSAGE,
    'api.common.unauthorized': UNAUTHORIZED_MESSAGE,
    'api.order.cancellation_window_closed':
      'The free cancellation window has closed.',
  };

  let showError: jest.Mock;
  let activeHttpMock: HttpTestingController | undefined;

  function setup(): { http: HttpClient; httpMock: HttpTestingController } {
    showError = jest.fn();
    TestBed.configureTestingModule({
      providers: [
        provideHttpClient(withInterceptors([HttpErrorInterceptorFn])),
        provideHttpClientTesting(),
        { provide: SnackbarService, useValue: { showError } },
        {
          provide: TranslateService,
          useValue: {
            // ngx-translate returns the key itself when no translation exists.
            instant: (key: string) => KNOWN_TRANSLATIONS[key] ?? key,
          },
        },
      ],
    });
    activeHttpMock = TestBed.inject(HttpTestingController);
    return {
      http: TestBed.inject(HttpClient),
      httpMock: activeHttpMock,
    };
  }

  function flushError(
    httpMock: HttpTestingController,
    body: { errors?: Record<string, string> },
    status = 400
  ): void {
    httpMock
      .expectOne(URL)
      .flush(body, { status, statusText: 'Bad Request' });
  }

  function flushBlobError(
    httpMock: HttpTestingController,
    url: string,
    body: string,
    status = 400
  ): void {
    httpMock
      .expectOne(url)
      .flush(new Blob([body], { type: 'application/json' }), {
        status,
        statusText: 'Bad Request',
      });
  }

  /** The blob read resolves on the FileReader's load event, a macrotask behind the flush. */
  async function settleBlobBranch(): Promise<void> {
    for (let tick = 0; tick < 5; tick++) {
      await new Promise((resolve) => setTimeout(resolve, 0));
    }
  }

  afterEach(() => {
    activeHttpMock?.verify();
    activeHttpMock = undefined;
    jest.restoreAllMocks();
  });

  it('resolves a known backend error key to its api.* translation', () => {
    const { http, httpMock } = setup();
    http.get(URL).subscribe({ error: () => undefined });
    flushError(httpMock, {
      errors: { order: 'order.cancellation_window_closed' },
    });

    expect(showError).toHaveBeenCalledWith(
      KNOWN_TRANSLATIONS['api.order.cancellation_window_closed']
    );
  });

  it('AC4: an unknown/unmapped error key falls back to the generic message, never the raw key', () => {
    const { http, httpMock } = setup();
    http.get(URL).subscribe({ error: () => undefined });
    flushError(httpMock, {
      errors: { something: 'totally.unknown_backend_code' },
    });

    expect(showError).toHaveBeenCalledWith(FALLBACK_MESSAGE);
    expect(showError).not.toHaveBeenCalledWith(
      'api.totally.unknown_backend_code'
    );
  });

  it('AC4: a response with no error codes falls back to the generic message', () => {
    const { http, httpMock } = setup();
    http.get(URL).subscribe({ error: () => undefined });
    flushError(httpMock, {});

    expect(showError).toHaveBeenCalledWith(FALLBACK_MESSAGE);
  });

  it('403 Forbidden surfaces the dedicated unauthorized message', () => {
    const { http, httpMock } = setup();
    http.get(URL).subscribe({ error: () => undefined });
    flushError(httpMock, { errors: { x: 'order.not_found' } }, 403);

    expect(showError).toHaveBeenCalledWith(UNAUTHORIZED_MESSAGE);
  });

  it('404 Not Found is intentionally silent (no snackbar)', () => {
    const { http, httpMock } = setup();
    http.get(URL).subscribe({ error: () => undefined });
    flushError(httpMock, { errors: { x: 'order.not_found' } }, 404);

    expect(showError).not.toHaveBeenCalled();
  });

  /**
   * The branch production actually takes: the generated clients read errors with
   * `responseType: 'blob'`, so a real refusal never reaches the object branch above. jsdom ships no
   * `Blob.prototype.text`, so without the shared `jest.polyfills.ts` every case here resolves through
   * the interceptor's `.catch` and reports the generic fallback — green for the wrong reason.
   */
  describe('a refusal that arrives as a Blob', () => {
    it('resolves the error key to its api.* translation', async () => {
      const { http, httpMock } = setup();
      http
        .get(URL, { responseType: 'blob' })
        .subscribe({ error: () => undefined });
      flushBlobError(
        httpMock,
        URL,
        JSON.stringify({ errors: { order: 'order.cancellation_window_closed' } })
      );
      await settleBlobBranch();

      expect(showError).toHaveBeenCalledWith(
        KNOWN_TRANSLATIONS['api.order.cancellation_window_closed']
      );
      expect(showError).not.toHaveBeenCalledWith(FALLBACK_MESSAGE);
    });

    it('falls back to the generic message for an unknown key, never the raw key', async () => {
      const { http, httpMock } = setup();
      http
        .get(URL, { responseType: 'blob' })
        .subscribe({ error: () => undefined });
      flushBlobError(
        httpMock,
        URL,
        JSON.stringify({ errors: { something: 'totally.unknown_backend_code' } })
      );
      await settleBlobBranch();

      expect(showError).toHaveBeenCalledWith(FALLBACK_MESSAGE);
      expect(showError).not.toHaveBeenCalledWith(
        'api.totally.unknown_backend_code'
      );
    });

    it('stays silent for an absent optional resource on a read', async () => {
      const READ_URL = '/api/Employee/GetMyPayoutDetails';
      const { http, httpMock } = setup();
      http
        .get(READ_URL, { responseType: 'blob' })
        .subscribe({ error: () => undefined });
      flushBlobError(
        httpMock,
        READ_URL,
        JSON.stringify({ errors: { PayoutDetailsNotFound: 'payout.not_found' } })
      );
      await settleBlobBranch();

      expect(showError).not.toHaveBeenCalled();
    });

    it('falls back to the generic message when the blob is not parseable JSON', async () => {
      const { http, httpMock } = setup();
      http
        .get(URL, { responseType: 'blob' })
        .subscribe({ error: () => undefined });
      flushBlobError(httpMock, URL, '<html>gateway timeout</html>');
      await settleBlobBranch();

      expect(showError).toHaveBeenCalledWith(FALLBACK_MESSAGE);
    });
  });

  /**
   * The opt-out a caller that promises silence needs. It is per request and defaults to off, so a
   * call site that says nothing keeps today's behaviour.
   */
  describe('SUPPRESS_ERROR_TOAST', () => {
    const suppressed = (): HttpContext =>
      new HttpContext().set(SUPPRESS_ERROR_TOAST, true);

    it('raises no snackbar for a request that opted out', () => {
      const { http, httpMock } = setup();
      http
        .get(URL, { context: suppressed() })
        .subscribe({ error: () => undefined });
      flushError(httpMock, {
        errors: { order: 'order.cancellation_window_closed' },
      });

      expect(showError).not.toHaveBeenCalled();
    });

    it('still raises the snackbar for the identical failure without the opt-out', () => {
      const { http, httpMock } = setup();
      http.get(URL).subscribe({ error: () => undefined });
      flushError(httpMock, {
        errors: { order: 'order.cancellation_window_closed' },
      });

      expect(showError).toHaveBeenCalledWith(
        KNOWN_TRANSLATIONS['api.order.cancellation_window_closed']
      );
    });

    it('covers a 403 too, which is a separate branch of the interceptor', () => {
      const { http, httpMock } = setup();
      http
        .get(URL, { context: suppressed() })
        .subscribe({ error: () => undefined });
      flushError(httpMock, { errors: { x: 'order.not_found' } }, 403);

      expect(showError).not.toHaveBeenCalled();
    });

    it('covers the blob branch, which is the one the generated clients take', async () => {
      const { http, httpMock } = setup();
      http
        .get(URL, { responseType: 'blob', context: suppressed() })
        .subscribe({ error: () => undefined });
      flushBlobError(
        httpMock,
        URL,
        JSON.stringify({ errors: { order: 'order.cancellation_window_closed' } })
      );
      await settleBlobBranch();

      expect(showError).not.toHaveBeenCalled();
    });

    it('still rethrows, so the caller decides what the failure means', () => {
      const { http, httpMock } = setup();
      let caught: unknown;
      http
        .get(URL, { context: suppressed() })
        .subscribe({ error: (error) => (caught = error) });
      flushError(httpMock, { errors: { order: 'order.not_found' } });

      expect(caught).toBeDefined();
    });
  });

  describe('an absent optional resource on a read', () => {
    const READ_URL = '/api/Employee/GetMyPayoutDetails';

    it('is silent — the caller renders the empty state instead', () => {
      const { http, httpMock } = setup();
      http.get(READ_URL).subscribe({ error: () => undefined });
      httpMock
        .expectOne(READ_URL)
        .flush(
          { errors: { PayoutDetailsNotFound: 'payout.not_found' } },
          { status: 400, statusText: 'Bad Request' }
        );

      expect(showError).not.toHaveBeenCalled();
    });

    it('still surfaces on a mutation, where the same code is a refusal', () => {
      const { http, httpMock } = setup();
      http.post(READ_URL, {}).subscribe({ error: () => undefined });
      httpMock
        .expectOne(READ_URL)
        .flush(
          { errors: { PayoutDetailsNotFound: 'payout.not_found' } },
          { status: 400, statusText: 'Bad Request' }
        );

      expect(showError).toHaveBeenCalledWith(FALLBACK_MESSAGE);
    });

    it('does not silence a different code on the same read', () => {
      const { http, httpMock } = setup();
      http.get(READ_URL).subscribe({ error: () => undefined });
      httpMock
        .expectOne(READ_URL)
        .flush(
          { errors: { EmployeeNotFound: 'employee.not_found' } },
          { status: 400, statusText: 'Bad Request' }
        );

      expect(showError).toHaveBeenCalledWith(FALLBACK_MESSAGE);
    });
  });
});
