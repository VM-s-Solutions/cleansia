import { Injectable, computed, inject, signal } from '@angular/core';
import {
  AdminRefundClient,
  IssuePartialRefundCommand,
  IssuePartialRefundRefundLineSelection,
  IssuePartialRefundResponse,
  RefundReason,
} from '@cleansia/admin-services';
import { UnsubscribeControlDirective } from '@cleansia/directives';
import { SnackbarService, extractApiErrorCode } from '@cleansia/services';
import { TranslateService } from '@ngx-translate/core';
import { catchError, finalize, of, takeUntil } from 'rxjs';
import {
  REFUND_ERROR_KEY_MAP,
  REFUND_FALLBACK_ERROR_KEY,
  RefundLineOption,
} from './admin-order-refund.models';

@Injectable()
export class AdminOrderRefundFacade extends UnsubscribeControlDirective {
  private readonly refundClient = inject(AdminRefundClient);
  private readonly snackbar = inject(SnackbarService);
  private readonly translate = inject(TranslateService);

  readonly lines = signal<RefundLineOption[]>([]);
  readonly reason = signal<RefundReason | null>(null);
  readonly overrideReason = signal<string>('');
  readonly submitting = signal<boolean>(false);
  readonly errorKey = signal<string | null>(null);

  readonly selectedLines = computed(() =>
    this.lines().filter((line) => line.selected)
  );
  readonly hasSelection = computed(() => this.selectedLines().length > 0);
  readonly canSubmit = computed(
    () => this.hasSelection() && this.reason() !== null && !this.submitting()
  );

  setLines(lines: RefundLineOption[]): void {
    this.lines.set(lines);
  }

  toggleLine(id: string, selected: boolean): void {
    this.lines.update((current) =>
      current.map((line) => (line.id === id ? { ...line, selected } : line))
    );
  }

  setReason(reason: RefundReason | null): void {
    this.reason.set(reason);
  }

  setOverrideReason(value: string): void {
    this.overrideReason.set(value);
  }

  submit(orderId: string, onSuccess: () => void): void {
    const reason = this.reason();
    if (!orderId || !this.hasSelection() || reason === null) {
      return;
    }

    this.errorKey.set(null);
    this.submitting.set(true);

    const command = new IssuePartialRefundCommand();
    command.orderId = orderId;
    command.reason = reason;
    command.overrideReason = this.overrideReason().trim() || undefined;
    command.lines = this.selectedLines().map((line) => {
      const selection = new IssuePartialRefundRefundLineSelection();
      selection.serviceId = line.id;
      selection.packageId = line.kind === 'bundled' ? line.packageId : undefined;
      return selection;
    });

    this.refundClient
      .partial(command)
      .pipe(
        takeUntil(this.destroyed$),
        catchError((error: unknown) => {
          this.errorKey.set(this.resolveErrorKey(error));
          return of(null);
        }),
        finalize(() => this.submitting.set(false))
      )
      .subscribe((response: IssuePartialRefundResponse | null) => {
        if (!response) {
          this.snackbar.showError(
            this.translate.instant(this.errorKey() ?? REFUND_FALLBACK_ERROR_KEY)
          );
          return;
        }
        this.snackbar.showSuccess(
          this.translate.instant('pages.order_management.refund.success')
        );
        this.reset();
        onSuccess();
      });
  }

  reset(): void {
    this.lines.update((current) =>
      current.map((line) => ({ ...line, selected: false }))
    );
    this.reason.set(null);
    this.overrideReason.set('');
    this.errorKey.set(null);
  }

  private resolveErrorKey(error: unknown): string {
    const code = extractApiErrorCode(error);
    if (code && REFUND_ERROR_KEY_MAP[code]) {
      return REFUND_ERROR_KEY_MAP[code];
    }
    return REFUND_FALLBACK_ERROR_KEY;
  }
}
