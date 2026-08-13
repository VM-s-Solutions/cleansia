export interface ChangePasswordFormData {
  currentPassword: string;
  newPassword: string;
}

// Mirrors the backend ChangeOwnPassword validator policy: minimum 8
// characters, at least one letter and one digit.
export const PASSWORD_PATTERN = /^(?=.*[a-zA-Z])(?=.*\d).{8,}$/;

export const CHANGE_PASSWORD_ERROR_KEY_MAP: Readonly<Record<string, string>> = {
  'auth.current_password_invalid': 'api.auth.current_password_invalid',
  'auth.invalid_password_format': 'api.auth.invalid_password_format',
  'common.required': 'api.common.required',
};

export const CHANGE_PASSWORD_FALLBACK_ERROR_KEY =
  'api.auth.change_password_failed';

export function resolveChangePasswordErrorKey(error: unknown): string {
  const apiError = error as {
    result?: { detail?: string; title?: string };
    response?: string;
  };
  let code = apiError?.result?.detail || apiError?.result?.title;

  if (!code && apiError?.response) {
    try {
      const parsed = JSON.parse(apiError.response) as {
        detail?: string;
        title?: string;
      };
      code = parsed.detail || parsed.title;
    } catch {
      code = undefined;
    }
  }

  if (code && CHANGE_PASSWORD_ERROR_KEY_MAP[code]) {
    return CHANGE_PASSWORD_ERROR_KEY_MAP[code];
  }
  return CHANGE_PASSWORD_FALLBACK_ERROR_KEY;
}
