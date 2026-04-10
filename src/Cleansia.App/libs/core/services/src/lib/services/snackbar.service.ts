import { Injectable, inject } from '@angular/core';
import { TranslateService } from '@ngx-translate/core';
import { MessageService } from 'primeng/api';

const DEFAULT_SNACKBAR_DURATION = 3_000;

interface ApiErrorResult {
  detail?: string;
  title?: string;
  errors?: Record<string, string[]>;
}

@Injectable({
  providedIn: 'root',
})
export class SnackbarService {
  private readonly messageService = inject(MessageService);
  private readonly translate = inject(TranslateService);

  showSuccess(message: string, duration?: number): void {
    this.showSnackbar(message, true, duration);
  }

  showError(message: string, duration?: number): void {
    this.showSnackbar(message, false, duration);
  }

  showSuccessTranslated(translationKey: string, duration?: number): void {
    const message = this.translate.instant(translationKey);
    this.showSnackbar(message, true, duration);
  }

  showErrorTranslated(translationKey: string, duration?: number): void {
    const message = this.translate.instant(translationKey);
    this.showSnackbar(message, false, duration);
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
   * Extracts the error message from an API exception and attempts to translate it.
   * Looks for the error message in the following order:
   * 1. result.detail (standard ASP.NET Core problem details)
   * 2. result.title
   * 3. error.message
   * 4. fallback translation key
   */
  extractApiErrorMessage(error: unknown, fallbackKey?: string): string {
    const fallbackMessage = fallbackKey
      ? this.translate.instant(fallbackKey)
      : this.translate.instant('api.common.error_occurred');

    if (!error) {
      return fallbackMessage;
    }

    const apiError = error as {
      result?: ApiErrorResult;
      message?: string;
      response?: string;
    };

    // Try to get the error detail from the result (ASP.NET Core problem details format)
    let errorDetail = apiError.result?.detail || apiError.result?.title;

    // If no detail in result, try parsing the response string
    if (!errorDetail && apiError.response) {
      try {
        const parsedResponse = JSON.parse(apiError.response) as ApiErrorResult;
        errorDetail = parsedResponse.detail || parsedResponse.title;
      } catch {
        // Response is not JSON, use it directly if it looks like an error message
        if (
          apiError.response &&
          !apiError.response.startsWith('<') &&
          apiError.response.length < 500
        ) {
          errorDetail = apiError.response;
        }
      }
    }

    // If no error detail found, use the error message or fallback
    if (!errorDetail) {
      return apiError.message || fallbackMessage;
    }

    // Try to translate the error detail as a key (e.g., "AfterPhotosRequiredToComplete" -> "api.order.after_photos.required")
    const translationKey = this.convertToTranslationKey(errorDetail);
    const translatedMessage = this.translate.instant(translationKey);

    // If translation key was found, use it; otherwise use the original error detail
    return translatedMessage !== translationKey
      ? translatedMessage
      : errorDetail;
  }

  /**
   * Converts a PascalCase or camelCase error message to a translation key.
   * E.g., "AfterPhotosRequiredToComplete" -> "api.order.after_photos.required"
   * This is a best-effort conversion for common patterns.
   */
  private convertToTranslationKey(errorMessage: string): string {
    // Common error message mappings
    const knownMappings: Record<string, string> = {
      afterphotosrequired: 'api.order.after_photos.required',
      afterphotosrequiredtocomplete: 'api.order.after_photos.required',
      ordernotinprogress: 'api.order.not_in_progress',
      ordernotconfirmed: 'api.order.not_confirmed',
      employeenotassigned: 'api.order.employee_not_assigned',
      employeealreadyassigned: 'api.order.employee_already_assigned',
      noavailablespots: 'api.order.no_available_spots',
      orderalreadyassigned: 'api.order.already_assigned',
      completionnotesrequired: 'api.order.completion_notes.required',
      actualtimemustbepositive: 'api.order.actual_time.positive',
      validationregistrationnumberinvalidformat:
        'api.validation.registration_number.invalid_format',
    };

    // Normalize the error message for lookup
    const normalizedMessage = errorMessage
      .toLowerCase()
      .replace(/[^a-z]/g, '');

    if (knownMappings[normalizedMessage]) {
      return knownMappings[normalizedMessage];
    }

    // Return the original message as a key (won't translate but won't break)
    return errorMessage;
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
