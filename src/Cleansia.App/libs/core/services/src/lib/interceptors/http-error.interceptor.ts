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
            parseBlobToJson(error.error)
              .then((parserErrorResponse) => {
                const errorKey = parserErrorResponse.errors
                  ? getObjectValues(parserErrorResponse.errors)[0]
                  : 'common.error_occurred';
                snackbarService.showError(translate.instant(`api.${errorKey}`));
              })
              .catch(() => {
                snackbarService.showError(
                  translate.instant('api.common.error_occurred')
                );
              });
          } else {
            // Handle non-blob errors directly
            const errorKey = error.error?.errors
              ? getObjectValues(error.error.errors)[0]
              : 'common.error_occurred';
            snackbarService.showError(translate.instant(`api.${errorKey}`));
          }
        }
      }
      return throwError(() => error);
    })
  );
};
