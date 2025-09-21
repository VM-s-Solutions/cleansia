import { Injectable, inject } from '@angular/core';
import { MessageService } from 'primeng/api';
import { TranslateService } from '@ngx-translate/core';

const DEFAULT_SNACKBAR_DURATION = 300000;

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
