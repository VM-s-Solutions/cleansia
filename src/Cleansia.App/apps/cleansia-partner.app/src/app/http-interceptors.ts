import { HttpInterceptorFn } from '@angular/common/http';
import { PARTNER_INTERCEPTORS_FN } from '@cleansia/partner-services';
import { PARTNER_STORE_INTERCEPTORS_FN } from '@cleansia/partner-stores';
import { COMMON_INTERCEPTORS_FN } from '@cleansia/services';

/**
 * The app is the only place that can see both the HTTP client lib and the
 * store lib, so it is where the chain is composed. Order is behaviour, not
 * style — `http-interceptors.spec.ts` pins it.
 */
export const APP_INTERCEPTORS_FN: HttpInterceptorFn[] = [
  ...COMMON_INTERCEPTORS_FN,
  ...PARTNER_INTERCEPTORS_FN,
  ...PARTNER_STORE_INTERCEPTORS_FN,
];
