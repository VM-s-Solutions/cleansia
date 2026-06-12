import {
  HttpErrorResponse,
  HttpInterceptorFn,
  HttpStatusCode,
} from '@angular/common/http';
import { isPlatformServer } from '@angular/common';
import { inject, PLATFORM_ID } from '@angular/core';
import { retry, throwError, timer } from 'rxjs';

const MAX_RETRIES = 1;
const DEFAULT_BACKOFF_MS = 60_000;
const JITTER_MAX_MS = 15_000;

export const retryAfterDelayMs = (
  retryAfterHeader: string | null,
  random: () => number = Math.random
): number => {
  const seconds = Number(retryAfterHeader);
  const baseMs =
    retryAfterHeader?.trim() && Number.isFinite(seconds) && seconds >= 0
      ? seconds * 1000
      : DEFAULT_BACKOFF_MS;
  return baseMs + random() * JITTER_MAX_MS;
};

export const RetryAfterInterceptorFn: HttpInterceptorFn = (req, next) => {
  if (isPlatformServer(inject(PLATFORM_ID))) {
    return next(req);
  }
  return next(req).pipe(
    retry({
      count: MAX_RETRIES,
      delay: (error: unknown) => {
        if (
          !(error instanceof HttpErrorResponse) ||
          error.status !== HttpStatusCode.TooManyRequests
        ) {
          return throwError(() => error);
        }
        return timer(retryAfterDelayMs(error.headers.get('Retry-After')));
      },
    })
  );
};
