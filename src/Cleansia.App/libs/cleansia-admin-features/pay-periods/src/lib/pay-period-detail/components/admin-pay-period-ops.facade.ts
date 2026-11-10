import { Injectable, computed, inject, signal } from '@angular/core';
import {
  AdminClient,
  ClosePayPeriodCommand,
  ClosePayPeriodResponse,
  MarkPayPeriodPaidCommand,
  MarkPayPeriodPaidResponse,
  PayPeriodDto,
  UpdatePayPeriodCommand,
  UpdatePayPeriodResponse,
} from '@cleansia/admin-services';
import { UnsubscribeControlDirective } from '@cleansia/directives';
import { resolveApiErrorKey, SnackbarService } from '@cleansia/services';
import { TranslateService } from '@ngx-translate/core';
import { Observable, catchError, finalize, of, takeUntil } from 'rxjs';
import { AdminPayPeriodOpsPanel, localDay } from './admin-pay-period-ops.models';

@Injectable()
export class AdminPayPeriodOpsFacade extends UnsubscribeControlDirective {
  private readonly adminClient = inject(AdminClient);
  private readonly snackbar = inject(SnackbarService);
  private readonly translate = inject(TranslateService);

  readonly activePanel = signal<AdminPayPeriodOpsPanel | null>(null);
  readonly submitting = signal<boolean>(false);
  readonly errorKey = signal<string | null>(null);

  readonly closeNowReason = signal<string>('');
  readonly canSubmitCloseNow = computed(
    () => this.closeNowReason().trim().length > 0 && !this.submitting()
  );

  readonly changeEndDate = signal<Date | null>(null);
  readonly canSubmitChangeEnd = computed(() => !!this.changeEndDate() && !this.submitting());

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

  setCloseNowReason(value: string): void {
    this.closeNowReason.set(value);
  }

  setChangeEndDate(value: Date | null): void {
    this.changeEndDate.set(value);
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

  closeNow(payPeriodId: string, onSuccess: () => void): void {
    const reason = this.closeNowReason().trim();
    if (!payPeriodId || !reason) {
      return;
    }
    const command = new ClosePayPeriodCommand();
    command.payPeriodId = payPeriodId;
    command.reason = reason;
    this.run(
      this.adminClient.adminPayPeriodClient.close(command),
      'pay_periods.messages.close_success',
      onSuccess
    );
  }

  // Cleaners receive a period's notes, so the change sends none and the stored ones stay.
  changeEnd(payPeriod: PayPeriodDto, onSuccess: () => void): void {
    const endDate = this.changeEndDate();
    if (!payPeriod.id || !endDate) {
      return;
    }
    const command = new UpdatePayPeriodCommand();
    command.payPeriodId = payPeriod.id;
    command.startDate = localDay(payPeriod.startDate);
    command.endDate = endDate;
    this.run(
      this.adminClient.adminPayPeriodClient.update(command),
      'pay_periods.detail.ops.change_end.success',
      onSuccess
    );
  }

  private run(
    request$: Observable<MarkPayPeriodPaidResponse | ClosePayPeriodResponse | UpdatePayPeriodResponse>,
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
    this.closeNowReason.set('');
    this.changeEndDate.set(null);
    this.errorKey.set(null);
  }

}
