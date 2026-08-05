import { HttpInterceptorFn } from '@angular/common/http';
import { CUSTOMER_INTERCEPTORS_FN } from '@cleansia/customer-services';
import { CUSTOMER_STORE_INTERCEPTORS_FN } from '@cleansia/customer-stores';
import { COMMON_INTERCEPTORS_FN } from '@cleansia/services';

/**
 * The app is the only place that can see both the HTTP client lib and the
 * store lib, so it is where the chain is composed. Order is behaviour, not
 * style — `http-interceptors.spec.ts` pins it.
 */
export const APP_INTERCEPTORS_FN: HttpInterceptorFn[] = [
  ...COMMON_INTERCEPTORS_FN,
  ...CUSTOMER_INTERCEPTORS_FN,
  ...CUSTOMER_STORE_INTERCEPTORS_FN,
];
