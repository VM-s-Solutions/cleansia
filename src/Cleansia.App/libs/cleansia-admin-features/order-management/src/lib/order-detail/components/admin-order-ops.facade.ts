import { Injectable, computed, inject, signal } from '@angular/core';
import {
  AdminCancelOrderAsNoShowCommand,
  AdminCancelOrderAsNoShowResponse,
  AdminCancelOrderCommand,
  AdminClient,
  AdminOverrideOrderStatusCommand,
  AdminReassignOrderCommand,
  AdminRefundOrderCommand,
  OrderStatus,
} from '@cleansia/admin-services';
import { UnsubscribeControlDirective } from '@cleansia/directives';
import { resolveApiErrorKey, SnackbarService } from '@cleansia/services';
import { formatMoney, localeFor } from '@cleansia/utils';
import { TranslateService } from '@ngx-translate/core';
import { Observable, catchError, finalize, of, takeUntil } from 'rxjs';
import { AdminOrderOpsPanel, NO_SHOW_OUTCOME_TOAST_MS } from './admin-order-ops.models';

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
  readonly fromEmployeeId = signal<string | null>(null);
  readonly toEmployeeId = signal<string>('');

  readonly canSubmitOverrideStatus = computed(
    () => this.targetStatus() !== null && !this.submitting()
  );
  /**
   * Only the DESTINATION is required. `fromEmployeeId` stays optional because the server's command
   * documents null as "a pure add into an open spot (no cleaner removed)" — and that is the case an
   * admin is called into: a job whose crew walked, or which nobody ever took, with nothing to
   * replace. Requiring a source here made the platform's own escalation path unreachable from the UI.
   */
  readonly canSubmitReassign = computed(
    () => this.toEmployeeId().trim().length > 0 && !this.submitting()
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

  setFromEmployeeId(value: string | null): void {
    this.fromEmployeeId.set(value);
  }

  setToEmployeeId(value: string): void {
    this.toEmployeeId.set(value);
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
    if (!orderId || !toEmployeeId) {
      return;
    }
    const command = new AdminReassignOrderCommand();
    command.orderId = orderId;
    // Undefined, not null, when nobody is being replaced: this is an ADD.
    command.fromEmployeeId = fromEmployeeId ?? undefined;
    command.toEmployeeId = toEmployeeId;
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

  private announceNoShow(
    outcome: AdminCancelOrderAsNoShowResponse,
    currencyCode: string | undefined
  ): void {
    const money = (amount: number) =>
      formatMoney(amount, currencyCode, localeFor(this.translate.currentLang), {
        fractionDigits: 2,
      });
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
    this.fromEmployeeId.set(null);
    this.toEmployeeId.set('');
    this.errorKey.set(null);
  }

}
