import {
  AuthInterceptorFn,
  PARTNER_INTERCEPTORS_FN,
  PartnerErrorInterceptorFn,
} from '@cleansia/partner-services';
import { LoadingInterceptorFn } from '@cleansia/partner-stores';
import {
  COMMON_INTERCEPTORS_FN,
  ContentDispositionInterceptorFn,
  HttpErrorInterceptorFn,
  RetryAfterInterceptorFn,
} from '@cleansia/services';
import { APP_INTERCEPTORS_FN } from './http-interceptors';

describe('partner HTTP interceptor chain', () => {
  it('registers the store-driving loading interceptor', () => {
    expect(APP_INTERCEPTORS_FN).toContain(LoadingInterceptorFn);
  });

  it('is exactly the common chain, then the client chain, then the store chain', () => {
    expect(APP_INTERCEPTORS_FN).toEqual([
      ContentDispositionInterceptorFn,
      HttpErrorInterceptorFn,
      RetryAfterInterceptorFn,
      AuthInterceptorFn,
      PartnerErrorInterceptorFn,
      LoadingInterceptorFn,
    ]);
  });

  it('reads a non-empty chain from each contributing lib', () => {
    expect(COMMON_INTERCEPTORS_FN.length).toBeGreaterThan(0);
    expect(PARTNER_INTERCEPTORS_FN.length).toBeGreaterThan(0);
    expect(APP_INTERCEPTORS_FN).toHaveLength(
      COMMON_INTERCEPTORS_FN.length + PARTNER_INTERCEPTORS_FN.length + 1
    );
  });
});
