import { Injectable, computed, inject, signal } from '@angular/core';
import {
  AdminCancelOrderAsLockoutCommand,
  AdminCancelOrderAsLockoutResponse,
  AdminCancelOrderAsNoShowCommand,
  AdminCancelOrderAsNoShowResponse,
  AdminCancelOrderCommand,
  AdminClient,
  AdminOverrideOrderStatusCommand,
  AdminReassignOrderCommand,
  AdminRecordCashReceivedCommand,
  AdminRefundOrderCommand,
  OrderStatus,
} from '@cleansia/admin-services';
import { UnsubscribeControlDirective } from '@cleansia/directives';
import { resolveApiErrorKey, SnackbarService } from '@cleansia/services';
import { formatMoney, localeFor } from '@cleansia/utils';
import { TranslateService } from '@ngx-translate/core';
import { Observable, catchError, finalize, of, takeUntil } from 'rxjs';
import { AdminOrderOpsPanel, NO_SHOW_OUTCOME_TOAST_MS } from './admin-order-ops.models';

function parseAmount(value: string): number | null {
  const trimmed = value.trim();
  if (!trimmed) {
    return null;
  }
  const parsed = Number(trimmed);
  return Number.isFinite(parsed) ? parsed : null;
}

@Injectable()
export class AdminOrderOpsFacade extends UnsubscribeControlDirective {
  private readonly adminClient = inject(AdminClient);
  private readonly snackbar = inject(SnackbarService);
  private readonly translate = inject(TranslateService);

  readonly activePanel = signal<AdminOrderOpsPanel | null>(null);
  readonly submitting = signal<boolean>(false);
  readonly errorKey = signal<string | null>(null);

  readonly cancelReason = signal<string>('');
  readonly targetStatus = signal<OrderStatus | null>(null);
  readonly overrideReason = signal<string>('');
  readonly fromEmployeeId = signal<string | null>(null);
  readonly toEmployeeId = signal<string>('');
  readonly removalReason = signal<string>('');
  readonly cashEmployeeId = signal<string | null>(null);
  readonly cashReceivedAt = signal<Date | null>(null);
  readonly cashAmount = signal<string>('');

  readonly canSubmitOverrideStatus = computed(
    () => this.targetStatus() !== null && !this.submitting()
  );
  readonly removesCleaner = computed(() => !!this.fromEmployeeId());
  /**
   * The DESTINATION is required, and a reason only when someone is taken off — the cleaner is shown
   * it. `fromEmployeeId` stays optional because the server's command
   * documents null as "a pure add into an open spot (no cleaner removed)" — and that is the case an
   * admin is called into: a job whose crew walked, or which nobody ever took, with nothing to
   * replace. Requiring a source here made the platform's own escalation path unreachable from the UI.
   */
  readonly canSubmitReassign = computed(
    () =>
      this.toEmployeeId().trim().length > 0 &&
      (!this.removesCleaner() || this.removalReason().trim().length > 0) &&
      !this.submitting()
  );
  readonly canSubmitRecordCash = computed(
    () =>
      !!this.cashEmployeeId() &&
      this.cashReceivedAt() !== null &&
      parseAmount(this.cashAmount()) !== null &&
      !this.submitting()
  );

  openPanel(panel: AdminOrderOpsPanel): void {
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

  setCancelReason(value: string): void {
    this.cancelReason.set(value);
  }

  setTargetStatus(value: OrderStatus | null): void {
    this.targetStatus.set(value);
  }

  setOverrideReason(value: string): void {
    this.overrideReason.set(value);
  }

  setFromEmployeeId(value: string | null): void {
    this.fromEmployeeId.set(value);
  }

  setToEmployeeId(value: string): void {
    this.toEmployeeId.set(value);
  }

  setRemovalReason(value: string): void {
    this.removalReason.set(value);
  }

  setCashEmployeeId(value: string | null): void {
    this.cashEmployeeId.set(value);
  }

  setCashReceivedAt(value: Date | null): void {
    this.cashReceivedAt.set(value);
  }

  setCashAmount(value: string): void {
    this.cashAmount.set(value);
  }

  cancelOrder(orderId: string, onSuccess: () => void): void {
    if (!orderId) {
      return;
    }
    const command = new AdminCancelOrderCommand();
    command.orderId = orderId;
    command.reason = this.cancelReason().trim() || undefined;
    this.run(
      this.adminClient.adminOrderClient.cancel(command),
      () =>
        this.snackbar.showSuccessTranslated(
          'pages.order_management.ops.cancel.success'
        ),
      onSuccess
    );
  }

  overrideStatus(orderId: string, onSuccess: () => void): void {
    const targetStatus = this.targetStatus();
    if (!orderId || targetStatus === null) {
      return;
    }
    const command = new AdminOverrideOrderStatusCommand();
    command.orderId = orderId;
    command.targetStatus = targetStatus;
    command.reason = this.overrideReason().trim() || undefined;
    this.run(
      this.adminClient.adminOrderClient.overrideStatus(command),
      () =>
        this.snackbar.showSuccessTranslated(
          'pages.order_management.ops.override_status.success'
        ),
      onSuccess
    );
  }

  reassignOrder(orderId: string, onSuccess: () => void): void {
    const fromEmployeeId = this.fromEmployeeId();
    const toEmployeeId = this.toEmployeeId().trim();
    const removalReason = fromEmployeeId ? this.removalReason().trim() : '';
    if (!orderId || !toEmployeeId || (fromEmployeeId && !removalReason)) {
      return;
    }
    const command = new AdminReassignOrderCommand();
    command.orderId = orderId;
    // Undefined, not null, when nobody is being replaced: this is an ADD.
    command.fromEmployeeId = fromEmployeeId ?? undefined;
    command.toEmployeeId = toEmployeeId;
    command.removalReason = removalReason || undefined;
    this.run(
      this.adminClient.adminOrderClient.reassign(command),
      () =>
        this.snackbar.showSuccessTranslated(
          'pages.order_management.ops.reassign.success'
        ),
      onSuccess
    );
  }

  refundOrder(orderId: string, onSuccess: () => void): void {
    if (!orderId) {
      return;
    }
    const command = new AdminRefundOrderCommand();
    command.orderId = orderId;
    this.run(
      this.adminClient.adminOrderClient.refund(command),
      () =>
        this.snackbar.showSuccessTranslated(
          'pages.order_management.ops.refund.success'
        ),
      onSuccess
    );
  }

  cancelAsNoShow(
    orderId: string,
    currencyCode: string | undefined,
    onSuccess: () => void
  ): void {
    if (!orderId) {
      return;
    }
    const command = new AdminCancelOrderAsNoShowCommand();
    command.orderId = orderId;
    this.run(
      this.adminClient.adminOrderClient.cancelNoShow(command),
      (outcome) => this.announceNoShow(outcome, currencyCode),
      onSuccess
    );
  }

  cancelAsLockout(
    orderId: string,
    currencyCode: string | undefined,
    onSuccess: () => void
  ): void {
    if (!orderId) {
      return;
    }
    const command = new AdminCancelOrderAsLockoutCommand();
    command.orderId = orderId;
    this.run(
      this.adminClient.adminOrderClient.cancelLockout(command),
      (outcome) => this.announceLockout(outcome, currencyCode),
      onSuccess
    );
  }

  recordCashReceived(orderId: string, onSuccess: () => void): void {
    const employeeId = this.cashEmployeeId();
    const receivedAt = this.cashReceivedAt();
    const amount = parseAmount(this.cashAmount());
    if (!orderId || !employeeId || receivedAt === null || amount === null) {
      return;
    }
    const command = new AdminRecordCashReceivedCommand();
    command.orderId = orderId;
    command.employeeId = employeeId;
    command.receivedAt = receivedAt;
    command.amount = amount;
    this.run(
      this.adminClient.adminOrderClient.recordCash(command),
      () =>
        this.snackbar.showSuccessTranslated(
          'pages.order_management.ops.record_cash.success'
        ),
      onSuccess
    );
  }

  private announceLockout(
    outcome: AdminCancelOrderAsLockoutResponse,
    currencyCode: string | undefined
  ): void {
    const receivable =
      typeof outcome.receivableAmount === 'number'
        ? this.translate.instant('pages.order_management.ops.lockout.receivable_opened', {
            amount: this.money(outcome.receivableAmount, currencyCode),
          })
        : this.translate.instant('pages.order_management.ops.lockout.no_receivable');
    this.snackbar.showSuccessTranslated(
      'pages.order_management.ops.lockout.success',
      { fee: this.money(outcome.feeAmount, currencyCode), receivable },
      NO_SHOW_OUTCOME_TOAST_MS
    );
  }

  private money(amount: number, currencyCode: string | undefined): string {
    return formatMoney(amount, currencyCode, localeFor(this.translate.currentLang), {
      fractionDigits: 2,
    });
  }

  private announceNoShow(
    outcome: AdminCancelOrderAsNoShowResponse,
    currencyCode: string | undefined
  ): void {
    const money = (amount: number) => this.money(amount, currencyCode);
    const refund =
      typeof outcome.refundedAmount === 'number'
        ? this.translate.instant('pages.order_management.ops.no_show.refunded', {
            amount: money(outcome.refundedAmount),
          })
        : this.translate.instant(
            outcome.refundPending
              ? 'pages.order_management.ops.no_show.refund_pending'
              : 'pages.order_management.ops.no_show.nothing_refunded'
          );
    const credit =
      typeof outcome.apologyCredit === 'number'
        ? this.translate.instant('pages.order_management.ops.no_show.credit_granted', {
            amount: money(outcome.apologyCredit),
          })
        : this.translate.instant('pages.order_management.ops.no_show.no_credit');
    this.snackbar.showSuccessTranslated(
      'pages.order_management.ops.no_show.success',
      { refund, credit },
      NO_SHOW_OUTCOME_TOAST_MS
    );
  }

  private run<T>(
    request$: Observable<T>,
    announce: (response: T) => void,
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
        announce(response);
        this.closePanel();
        onSuccess();
      });
  }

  private resetInputs(): void {
    this.cancelReason.set('');
    this.targetStatus.set(null);
    this.overrideReason.set('');
    this.fromEmployeeId.set(null);
    this.toEmployeeId.set('');
    this.removalReason.set('');
    this.cashEmployeeId.set(null);
    this.cashReceivedAt.set(null);
    this.cashAmount.set('');
    this.errorKey.set(null);
  }

}
