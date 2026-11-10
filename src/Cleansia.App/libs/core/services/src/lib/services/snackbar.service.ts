import { Injectable, inject } from '@angular/core';
import { TranslateService } from '@ngx-translate/core';
import { MessageService } from 'primeng/api';
import { extractApiErrorCode, GENERIC_API_ERROR_KEY } from './api-error';

const DEFAULT_SNACKBAR_DURATION = 3_000;

/**
 * Normalized API error keys (lowercase, alpha-only) to translation keys, read by `showApiError`
 * for the cross-app order, validation and auth codes that do not mirror a bundle key.
 */
export const DEFAULT_SNACKBAR_ERROR_MAPPINGS: Readonly<Record<string, string>> = {
  afterphotosrequired: 'api.order.after_photos.required',
  afterphotosrequiredtocomplete: 'api.order.after_photos.required',
  ordernotinprogress: 'api.order.not_in_progress',
  ordernotconfirmed: 'api.order.not_confirmed',
  employeenotassigned: 'api.order.employee_not_assigned',
  employeealreadyassigned: 'api.order.employee_already_assigned',
  noavailablespots: 'api.order.no_available_spots',
  validationregistrationnumberinvalidformat:
    'api.validation.registration_number.invalid_format',
  // Service-areas: backend rejects address writes when the country/city
  // isn't part of the service area. The keys below are the normalized form
  // (lowercase, alphanumeric only) of BusinessErrorMessage constants
  // 'country.not_serviced', 'country.required', 'city.not_serviced'.
  countrynotserviced: 'api.service_area.country_not_serviced',
  countryrequired: 'api.service_area.country_required',
  citynotserviced: 'api.service_area.city_not_serviced',
  // Provider mismatch on password login — the backend names the provider the
  // account actually uses ('auth.google_type_error', 'auth.apple_type_error',
  // the neutral 'auth.external_type_error', and the mirror case
  // 'auth.internal_type_error'). The login facades report through
  // `showApiError`. The HTTP interceptor resolves the same keys under `api.` itself.
  authgoogletypeerror: 'api.auth.google_type_error',
  authappletypeerror: 'api.auth.apple_type_error',
  authexternaltypeerror: 'api.auth.external_type_error',
  authinternaltypeerror: 'api.auth.internal_type_error',
  // Password reset. The forgot-password facades report through `showApiError`
  // too, and `showSnackbar` clears the message queue — so the facade's snackbar
  // replaces the interceptor's already-translated one and is the message the
  // user actually reads.
  authinvalidresettoken: 'api.auth.invalid_reset_token',
  authsameresetpassword: 'api.auth.same_reset_password',
  // Social sign-in that matches no account. The sign-in pair asserts no terms
  // tick, so the backend refuses rather than provisioning, and the login
  // facade's showApiError replaces the interceptor's translated snackbar.
  authsocialaccountnotfound: 'api.auth.social_account_not_found',
  // A missed order lookup. The admin incident-file scope is an order id typed by
  // hand, so 'order.not_found' is a routine refusal there, and the facade's
  // showApiError is the snackbar the admin reads.
  ordernotfound: 'api.order.not_found',
};

@Injectable({
  providedIn: 'root',
})
export class SnackbarService {
  private readonly messageService = inject(MessageService);
  private readonly translate = inject(TranslateService);
  private readonly errorMappings = DEFAULT_SNACKBAR_ERROR_MAPPINGS;

  showSuccess(message: string, duration?: number): void {
    this.showSnackbar(message, true, duration);
  }

  showError(message: string, duration?: number): void {
    this.showSnackbar(message, false, duration);
  }

  showSuccessTranslated(
    translationKey: string,
    params?: Record<string, unknown>,
    duration?: number
  ): void {
    this.showSnackbar(this.translate.instant(translationKey, params), true, duration);
  }

  showErrorTranslated(
    translationKey: string,
    params?: Record<string, unknown>,
    duration?: number
  ): void {
    this.showSnackbar(this.translate.instant(translationKey, params), false, duration);
  }

  showInfoTranslated(translationKey: string, duration?: number): void {
    const message = this.translate.instant(translationKey);
    this.showSnackbar(message, true, duration);
  }

  /**
   * Shows an error from an API exception with proper translation.
   * Extracts the error message from ApiException and attempts to translate it.
   * @param error The API error (ApiException or any error object)
   * @param fallbackKey Optional translation key to use if error extraction fails
   * @param duration Optional duration for the snackbar
   */
  showApiError(
    error: unknown,
    fallbackKey?: string,
    duration?: number
  ): void {
    const errorMessage = this.extractApiErrorMessage(error, fallbackKey);
    this.showSnackbar(errorMessage, false, duration);
  }

  /**
   * Resolves an API failure to a translated message: the code through the error map, then
   * `api.<code>`, then the code as a bundle key, then the caller's fallback, then the generic
   * message. A raw code, response text or exception message never reaches the screen.
   */
  extractApiErrorMessage(error: unknown, fallbackKey?: string): string {
    const code = error ? extractApiErrorCode(error) : undefined;
    if (code) {
      const mapped = this.errorMappings[code.toLowerCase().replace(/[^a-z]/g, '')];
      for (const key of [mapped, `api.${code}`, code]) {
        if (!key) continue;
        const message = this.translate.instant(key);
        if (message !== key) return message;
      }
    }

    return this.translate.instant(fallbackKey ?? GENERIC_API_ERROR_KEY);
  }

  private showSnackbar(
    message: string,
    success: boolean,
    duration?: number
  ): void {
    this.messageService.clear();

    const severity = success ? 'success' : 'error';
    const summary = success
      ? this.translate.instant('global.messages.success')
      : this.translate.instant('global.messages.error');
    this.messageService.add({
      severity,
      summary,
      detail: message,
      life: duration ?? DEFAULT_SNACKBAR_DURATION,
    });
  }
}
