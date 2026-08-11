import { HttpInterceptorFn } from '@angular/common/http';
import { inject } from '@angular/core';
import { Store } from '@ngrx/store';
import { finalize } from 'rxjs';
import { setLoadingOffAction, setLoadingOnAction } from './loading.actions';

/**
 * Lives beside the actions it dispatches, not in `admin-services`: an
 * interceptor that drives the store sits ABOVE the store, and the store already
 * reads the generated client. Registering it from `admin-services` put the only
 * `admin-services -> admin-stores` arrow in the workspace and made the pair
 * circular.
 */
export const LoadingInterceptorFn: HttpInterceptorFn = (req, next) => {
  const store = inject(Store);

  store.dispatch(setLoadingOnAction());
  return next(req).pipe(
    finalize(() => {
      store.dispatch(setLoadingOffAction());
    })
  );
};

export const ADMIN_STORE_INTERCEPTORS_FN = [LoadingInterceptorFn];
