import { HttpInterceptorFn } from '@angular/common/http';
import { inject } from '@angular/core';
import {
  setLoadingOffAction,
  setLoadingOnAction,
} from '@cleansia/partner-stores';
import { Store } from '@ngrx/store';
import { finalize } from 'rxjs';

export const LoadingInterceptorFn: HttpInterceptorFn = (req, next) => {
  const store = inject(Store);

  store.dispatch(setLoadingOnAction());
  return next(req).pipe(
    finalize(() => {
      store.dispatch(setLoadingOffAction());
    })
  );
};
