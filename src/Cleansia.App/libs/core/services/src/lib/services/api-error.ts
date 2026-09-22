import { TranslateService } from '@ngx-translate/core';

/**
 * Business codes that report an *optional* resource the caller has simply not
 * created yet. On a read they are an empty state the caller renders, not a
 * failure — the same reason the shared error interceptor is silent on a 404.
 * On a mutation the identical code is a refusal and still surfaces.
 */
export const ABSENT_RESOURCE_ERROR_CODES: ReadonlySet<string> = new Set([
  // A cleaner who has never saved a payout destination (ADR-0034 D8.2).
  'payout.not_found',
]);

export interface ApiErrorResult {
  detail?: string;
  title?: string;
  /**
   * Keyed by the failing field or error code, valued with the business error
   * key. `CleansiaApiController.CreateProblemDetails` joins several messages
   * for one code with '; ' into a single string; ASP.NET's own model-binding
   * ProblemDetails uses an array instead — both shapes reach us.
   */
  errors?: Record<string, string | string[]>;
}

/**
 * On the validation arm of `CleansiaApiController.HandleFailure`, `detail`
 * holds the sentinel 'A validation problem occurred.' and only `errors` carries
 * the business key, so `errors` has to win. On the auth arm both carry the same
 * key, and on the plain bad-request arm `errors` is empty — hence the fallback.
 */
function firstErrorKey(result: ApiErrorResult | undefined): string | undefined {
  for (const value of Object.values(result?.errors ?? {})) {
    const key = Array.isArray(value) ? value[0] : value;
    if (key) {
      return key;
    }
  }

  return undefined;
}

export function extractApiErrorCode(error: unknown): string | undefined {
  const apiError = error as ApiErrorResult & {
    result?: ApiErrorResult;
    response?: string;
  };

  // The same failure reaches us in two shapes. NSwag's `throwException` throws
  // the parsed ProblemDetails BARE whenever the response had a body — which is
  // every 400 and 401 this API returns — and only wraps it in an ApiException
  // (where the body lands on `.result`) when it did not. Reading `.result`
  // alone therefore misses every business error the generated clients raise.
  for (const result of [apiError?.result, apiError]) {
    const code = firstErrorKey(result) || result?.detail || result?.title;
    if (code) {
      return code;
    }
  }

  if (apiError?.response) {
    try {
      const parsed = JSON.parse(apiError.response) as ApiErrorResult;
      return firstErrorKey(parsed) || parsed.detail || parsed.title || undefined;
    } catch {
      return undefined;
    }
  }

  return undefined;
}

export const GENERIC_API_ERROR_KEY = 'api.common.error_occurred';

/**
 * The `api.<code>` key a failure translates to, for a message rendered in place rather than
 * toasted — the toast itself is the interceptor's. An untranslated code falls back so a raw
 * machine key never reaches the screen.
 */
export function resolveApiErrorKey(
  translate: TranslateService,
  error: unknown,
  fallbackKey: string = GENERIC_API_ERROR_KEY
): string {
  const code = extractApiErrorCode(error);
  if (!code) {
    return fallbackKey;
  }
  const key = `api.${code}`;
  return translate.instant(key) === key ? fallbackKey : key;
}
