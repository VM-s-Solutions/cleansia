import {
  HttpClient,
  HttpContextToken,
  HttpHandler,
} from '@angular/common/http';
import { inject } from '@angular/core';

/**
 * Opts a single request out of `HttpErrorInterceptorFn`'s snackbar. It defaults to `false`, so a
 * call site that says nothing keeps the shared behaviour; the error still reaches the caller.
 */
export const SUPPRESS_ERROR_TOAST = new HttpContextToken<boolean>(() => false);

/**
 * The NSwag clients build their own request options and expose no seam for an `HttpContext`, so a
 * call site opts out by constructing its generated sub-client over this `HttpClient` rather than
 * over the ambient one. It stamps the token and delegates to the same interceptor chain, so every
 * other interceptor — auth, retry, loading — behaves identically.
 *
 * Must be called in an injection context.
 */
export function errorToastSuppressingHttpClient(): HttpClient {
  const chain = inject(HttpHandler);

  return new HttpClient({
    handle: (request) =>
      chain.handle(
        request.clone({
          context: request.context.set(SUPPRESS_ERROR_TOAST, true),
        })
      ),
  });
}
