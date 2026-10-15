import {
  CUSTOMER_INTERCEPTORS_FN,
  CustomerAuthInterceptorFn,
  CustomerErrorInterceptorFn,
} from '@cleansia/customer-services';
import {
  CUSTOMER_STORE_INTERCEPTORS_FN,
  CustomerLoadingInterceptorFn,
} from '@cleansia/customer-stores';
import {
  COMMON_INTERCEPTORS_FN,
  ContentDispositionInterceptorFn,
  HttpErrorInterceptorFn,
  RetryAfterInterceptorFn,
} from '@cleansia/services';
import { APP_INTERCEPTORS_FN } from './http-interceptors';
import { SsrApiDeadlineInterceptorFn } from './ssr/api-deadline.interceptor';

describe('customer HTTP interceptor chain', () => {
  it('registers the store-driving loading interceptor', () => {
    expect(APP_INTERCEPTORS_FN).toContain(CustomerLoadingInterceptorFn);
  });

  it('is the common, client and store chains followed by the server deadline', () => {
    expect(APP_INTERCEPTORS_FN).toEqual([
      ContentDispositionInterceptorFn,
      HttpErrorInterceptorFn,
      RetryAfterInterceptorFn,
      CustomerAuthInterceptorFn,
      CustomerErrorInterceptorFn,
      CustomerLoadingInterceptorFn,
      SsrApiDeadlineInterceptorFn,
    ]);
  });

  it('reads a non-empty chain from each contributing lib', () => {
    expect(COMMON_INTERCEPTORS_FN.length).toBeGreaterThan(0);
    expect(CUSTOMER_INTERCEPTORS_FN.length).toBeGreaterThan(0);
    expect(APP_INTERCEPTORS_FN).toHaveLength(
      COMMON_INTERCEPTORS_FN.length +
        CUSTOMER_INTERCEPTORS_FN.length +
        CUSTOMER_STORE_INTERCEPTORS_FN.length +
        1
    );
  });
});
