import {
  HttpErrorResponse,
  HttpInterceptorFn,
  HttpStatusCode,
} from '@angular/common/http';
import { inject } from '@angular/core';
import { getObjectValues, parseBlobToJson } from '@cleansia/utils';
import { TranslateService } from '@ngx-translate/core';
import { catchError, throwError } from 'rxjs';
import { SnackbarService } from '../services';

const GENERIC_ERROR_KEY = 'api.common.error_occurred';

function resolveApiError(translate: TranslateService, errorKey: unknown): string {
  const candidateKey = `api.${String(errorKey)}`;
  const message = translate.instant(candidateKey);
  // ngx-translate echoes the key back when it has no translation — never let a
  // raw machine key reach the snackbar; fall back to the generic message.
  return message === candidateKey ? translate.instant(GENERIC_ERROR_KEY) : message;
}

export const HttpErrorInterceptorFn: HttpInterceptorFn = (req, next) => {
  const snackbarService = inject(SnackbarService);
  const translate = inject(TranslateService);
  return next(req).pipe(
    catchError((error: HttpErrorResponse) => {
      if (!error.ok && error.status !== HttpStatusCode.NotFound) {
        if (error.status === HttpStatusCode.Forbidden) {
          snackbarService.showError(
            translate.instant('api.common.unauthorized')
          );
        } else {
          if (error.error instanceof Blob) {
            parseBlobToJson<{ errors?: Record<string, string> }>(error.error)
              .then((parserErrorResponse) => {
                const errorKey = parserErrorResponse.errors
                  ? getObjectValues(parserErrorResponse.errors)[0]
                  : 'common.error_occurred';
                snackbarService.showError(resolveApiError(translate, errorKey));
              })
              .catch(() => {
                snackbarService.showError(translate.instant(GENERIC_ERROR_KEY));
              });
          } else {
            const errorKey = error.error?.errors
              ? getObjectValues(error.error.errors)[0]
              : 'common.error_occurred';
            snackbarService.showError(resolveApiError(translate, errorKey));
          }
        }
      }
      return throwError(() => error);
    })
  );
};
