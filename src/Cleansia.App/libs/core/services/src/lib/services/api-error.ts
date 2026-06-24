export interface ApiErrorResult {
  detail?: string;
  title?: string;
  errors?: Record<string, string[]>;
}

export function extractApiErrorCode(error: unknown): string | undefined {
  const apiError = error as { result?: ApiErrorResult; response?: string };
  const code = apiError?.result?.detail || apiError?.result?.title;
  if (code) {
    return code;
  }

  if (apiError?.response) {
    try {
      const parsed = JSON.parse(apiError.response) as ApiErrorResult;
      return parsed.detail || parsed.title || undefined;
    } catch {
      return undefined;
    }
  }

  return undefined;
}
