import { Injectable, inject } from '@angular/core';
import { TranslateService } from '@ngx-translate/core';
import { MessageService } from 'primeng/api';

const DEFAULT_SNACKBAR_DURATION = 3_000;

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
