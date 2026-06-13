import {
  HttpClient,
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
});
