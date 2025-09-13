import { Injectable } from '@angular/core';
import { MessageService } from 'primeng/api';

const DEFAULT_SNACKBAR_DURATION = 3000;

@Injectable({
  providedIn: 'root',
})
export class SnackbarService {
  constructor(private messageService: MessageService) {}

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
    this.messageService.add({
      severity,
      detail: message,
      life: duration ?? DEFAULT_SNACKBAR_DURATION,
    });
  }
}
