import { Injectable, computed, inject, signal } from '@angular/core';
import {
  AdminCancelOrderCommand,
  AdminCancelOrderResponse,
  AdminClient,
  AdminOverrideOrderStatusCommand,
  AdminOverrideOrderStatusResponse,
  AdminReassignOrderCommand,
  AdminReassignOrderResponse,
  AdminRefundOrderCommand,
  AdminRefundOrderResponse,
  OrderStatus,
} from '@cleansia/admin-services';
import { UnsubscribeControlDirective } from '@cleansia/directives';
import { SnackbarService } from '@cleansia/services';
import { TranslateService } from '@ngx-translate/core';
import { Observable, catchError, finalize, of, takeUntil } from 'rxjs';
import {
  AdminOrderOpsPanel,
  ORDER_OPS_ERROR_KEY_MAP,
  ORDER_OPS_FALLBACK_ERROR_KEY,
} from './admin-order-ops.models';

interface ApiErrorResult {
  detail?: string;
  title?: string;
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
  readonly fromEmployeeId = signal<string | null>(null);
  readonly toEmployeeId = signal<string>('');

  readonly canSubmitOverrideStatus = computed(
    () => this.targetStatus() !== null && !this.submitting()
  );
  readonly canSubmitReassign = computed(
    () =>
      this.fromEmployeeId() !== null &&
      this.toEmployeeId().trim().length > 0 &&
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
    const command = new AdminCancelOrderCommand({
      orderId,
      reason: this.cancelReason().trim() || undefined,
    });
    this.run(
      this.adminClient.adminOrderClient.cancel(command),
      'pages.order_management.ops.cancel.success',
      onSuccess
    );
  }

  overrideStatus(orderId: string, onSuccess: () => void): void {
    const targetStatus = this.targetStatus();
    if (!orderId || targetStatus === null) {
      return;
    }
    const command = new AdminOverrideOrderStatusCommand({
      orderId,
      targetStatus,
    });
    this.run(
      this.adminClient.adminOrderClient.overrideStatus(command),
      'pages.order_management.ops.override_status.success',
      onSuccess
    );
  }

  reassignOrder(orderId: string, onSuccess: () => void): void {
    const fromEmployeeId = this.fromEmployeeId();
    const toEmployeeId = this.toEmployeeId().trim();
    if (!orderId || fromEmployeeId === null || !toEmployeeId) {
      return;
    }
    const command = new AdminReassignOrderCommand({
      orderId,
      fromEmployeeId,
      toEmployeeId,
    });
    this.run(
      this.adminClient.adminOrderClient.reassign(command),
      'pages.order_management.ops.reassign.success',
      onSuccess
    );
  }

  refundOrder(orderId: string, onSuccess: () => void): void {
    if (!orderId) {
      return;
    }
    const command = new AdminRefundOrderCommand({ orderId });
    this.run(
      this.adminClient.adminOrderClient.refund(command),
      'pages.order_management.ops.refund.success',
      onSuccess
    );
  }

  private run(
    request$: Observable<
      | AdminCancelOrderResponse
      | AdminOverrideOrderStatusResponse
      | AdminReassignOrderResponse
      | AdminRefundOrderResponse
    >,
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
              this.errorKey() ?? ORDER_OPS_FALLBACK_ERROR_KEY
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
    this.cancelReason.set('');
    this.targetStatus.set(null);
    this.fromEmployeeId.set(null);
    this.toEmployeeId.set('');
    this.errorKey.set(null);
  }

  private resolveErrorKey(error: unknown): string {
    const apiError = error as { result?: ApiErrorResult; response?: string };
    let code = apiError?.result?.detail || apiError?.result?.title;

    if (!code && apiError?.response) {
      try {
        const parsed = JSON.parse(apiError.response) as ApiErrorResult;
        code = parsed.detail || parsed.title;
      } catch {
        code = undefined;
      }
    }

    if (code && ORDER_OPS_ERROR_KEY_MAP[code]) {
      return ORDER_OPS_ERROR_KEY_MAP[code];
    }
    return ORDER_OPS_FALLBACK_ERROR_KEY;
  }
}
