import { HttpInterceptorFn } from '@angular/common/http';
import { inject } from '@angular/core';
import { Store } from '@ngrx/store';
import { finalize } from 'rxjs';
import {
  setCustomerLoadingOffAction,
  setCustomerLoadingOnAction,
} from './loading.actions';

/**
 * Lives beside the actions it dispatches, not in `customer-services`: an
 * interceptor that drives the store sits ABOVE the store, and the store already
 * reads the generated client. Registering it from `customer-services` was one
 * of the two arrows that made the pair circular.
 */
export const CustomerLoadingInterceptorFn: HttpInterceptorFn = (req, next) => {
  const store = inject(Store);

  store.dispatch(setCustomerLoadingOnAction());
  return next(req).pipe(
    finalize(() => {
      store.dispatch(setCustomerLoadingOffAction());
    })
  );
};

export const CUSTOMER_STORE_INTERCEPTORS_FN = [CustomerLoadingInterceptorFn];
