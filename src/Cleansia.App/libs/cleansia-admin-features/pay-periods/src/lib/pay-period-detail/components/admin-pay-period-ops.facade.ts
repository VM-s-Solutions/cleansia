import { Injectable, inject, signal } from '@angular/core';
import {
  AdminClient,
  MarkPayPeriodPaidCommand,
  MarkPayPeriodPaidResponse,
  ReopenPayPeriodCommand,
  ReopenPayPeriodResponse,
} from '@cleansia/admin-services';
import { UnsubscribeControlDirective } from '@cleansia/directives';
import { resolveApiErrorKey, SnackbarService } from '@cleansia/services';
import { TranslateService } from '@ngx-translate/core';
import { Observable, catchError, finalize, of, takeUntil } from 'rxjs';
import { AdminPayPeriodOpsPanel } from './admin-pay-period-ops.models';

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
    const command = new MarkPayPeriodPaidCommand();
    command.payPeriodId = payPeriodId;
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
    const command = new ReopenPayPeriodCommand();
    command.payPeriodId = payPeriodId;
    command.notes = this.reopenNotes().trim() || undefined;
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
          this.errorKey.set(resolveApiErrorKey(this.translate, error));
          return of(null);
        }),
        finalize(() => this.submitting.set(false))
      )
      .subscribe((response) => {
        if (!response) return;
        this.snackbar.showSuccessTranslated(successKey);
        this.closePanel();
        onSuccess();
      });
  }

  private resetInputs(): void {
    this.reopenNotes.set('');
    this.errorKey.set(null);
  }

}
