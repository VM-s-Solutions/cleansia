import { Injectable, inject, signal } from '@angular/core';
import {
  AdminClient,
  MarkPayPeriodPaidCommand,
  MarkPayPeriodPaidResponse,
  ReopenPayPeriodCommand,
  ReopenPayPeriodResponse,
} from '@cleansia/admin-services';
import { UnsubscribeControlDirective } from '@cleansia/directives';
import { SnackbarService, extractApiErrorCode } from '@cleansia/services';
import { TranslateService } from '@ngx-translate/core';
import { Observable, catchError, finalize, of, takeUntil } from 'rxjs';
import {
  AdminPayPeriodOpsPanel,
  PAY_PERIOD_OPS_ERROR_KEY_MAP,
  PAY_PERIOD_OPS_FALLBACK_ERROR_KEY,
} from './admin-pay-period-ops.models';

@Injectable()
export class AdminPayPeriodOpsFacade extends UnsubscribeControlDirective {
  private readonly adminClient = inject(AdminClient);
  private readonly snackbar = inject(SnackbarService);
  private readonly translate = inject(TranslateService);

  readonly activePanel = signal<AdminPayPeriodOpsPanel | null>(null);
  readonly submitting = signal<boolean>(false);
  readonly errorKey = signal<string | null>(null);

  readonly reopenNotes = signal<string>('');

  openPanel(panel: AdminPayPeriodOpsPanel): void {
    if (this.activePanel() === panel) {
      this.closePanel();
      return;
    }
    this.resetInputs();
    this.activePanel.set(panel);
  }

  closePanel(): void {
    this.activePanel.set(null);
    this.resetInputs();
  }

  setReopenNotes(value: string): void {
    this.reopenNotes.set(value);
  }

  markPaid(payPeriodId: string, onSuccess: () => void): void {
    if (!payPeriodId) {
      return;
    }
    const command = new MarkPayPeriodPaidCommand({ payPeriodId });
    this.run(
      this.adminClient.adminPayPeriodClient.markPaid(command),
      'pay_periods.detail.ops.mark_paid.success',
      onSuccess
    );
  }

  reopen(payPeriodId: string, onSuccess: () => void): void {
    if (!payPeriodId) {
      return;
    }
    const command = new ReopenPayPeriodCommand({
      payPeriodId,
      notes: this.reopenNotes().trim() || undefined,
    });
    this.run(
      this.adminClient.adminPayPeriodClient.reopen(command),
      'pay_periods.detail.ops.reopen.success',
      onSuccess
    );
  }

  private run(
    request$: Observable<MarkPayPeriodPaidResponse | ReopenPayPeriodResponse>,
    successKey: string,
    onSuccess: () => void
  ): void {
    this.errorKey.set(null);
    this.submitting.set(true);

    request$
      .pipe(
        takeUntil(this.destroyed$),
        catchError((error: unknown) => {
          this.errorKey.set(this.resolveErrorKey(error));
          return of(null);
        }),
        finalize(() => this.submitting.set(false))
      )
      .subscribe((response) => {
        if (!response) {
          this.snackbar.showError(
            this.translate.instant(
              this.errorKey() ?? PAY_PERIOD_OPS_FALLBACK_ERROR_KEY
            )
          );
          return;
        }
        this.snackbar.showSuccess(this.translate.instant(successKey));
        this.closePanel();
        onSuccess();
      });
  }

  private resetInputs(): void {
    this.reopenNotes.set('');
    this.errorKey.set(null);
  }

  private resolveErrorKey(error: unknown): string {
    const code = extractApiErrorCode(error);
    if (code && PAY_PERIOD_OPS_ERROR_KEY_MAP[code]) {
      return PAY_PERIOD_OPS_ERROR_KEY_MAP[code];
    }
    return PAY_PERIOD_OPS_FALLBACK_ERROR_KEY;
  }
}
