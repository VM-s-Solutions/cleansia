export const ADMIN_USER_FORM_ERROR_KEY_MAP: Readonly<Record<string, string>> = {
  'admin_user.email_exists': 'errors.admin_user.email_exists',
  'admin_user.not_found': 'errors.admin_user.not_found',
  'language.not_supported': 'errors.language.not_supported',
  'validation.date_must_be_in_past': 'errors.validation.date_must_be_in_past',
  'validation.invalid_age': 'errors.validation.invalid_age',
  'user.existing_email': 'errors.user.existing_email',
  'user.existing_phone_number': 'errors.user.existing_phone_number',
};

export const ADMIN_USER_FORM_FALLBACK_ERROR_KEY = 'errors.common.error_occurred';

export function resolveAdminUserFormErrorKey(error: unknown): string {
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

  if (code && ADMIN_USER_FORM_ERROR_KEY_MAP[code]) {
    return ADMIN_USER_FORM_ERROR_KEY_MAP[code];
  }
  return ADMIN_USER_FORM_FALLBACK_ERROR_KEY;
}
