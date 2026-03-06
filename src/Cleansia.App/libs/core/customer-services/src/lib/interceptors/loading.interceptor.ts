import { HttpInterceptorFn } from '@angular/common/http';
import { inject } from '@angular/core';
import { Store } from '@ngrx/store';
import { finalize } from 'rxjs';
import {
  setCustomerLoadingOffAction,
  setCustomerLoadingOnAction,
} from '@cleansia/customer-stores';

export const CustomerLoadingInterceptorFn: HttpInterceptorFn = (req, next) => {
  const store = inject(Store);

  store.dispatch(setCustomerLoadingOnAction());
  return next(req).pipe(
    finalize(() => {
      store.dispatch(setCustomerLoadingOffAction());
    })
  );
};
