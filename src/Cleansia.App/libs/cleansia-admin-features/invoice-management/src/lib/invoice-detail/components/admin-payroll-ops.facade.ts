import { Injectable, computed, inject, signal } from '@angular/core';
import {
  AdminClient,
  DisputeInvoiceCommand,
  DisputeInvoiceResponse,
  RejectInvoiceCommand,
  RejectInvoiceResponse,
  UpdateInvoiceAmountsCommand,
  UpdateInvoiceAmountsResponse,
} from '@cleansia/admin-services';
import { UnsubscribeControlDirective } from '@cleansia/directives';
import { SnackbarService, extractApiErrorCode } from '@cleansia/services';
import { TranslateService } from '@ngx-translate/core';
import { Observable, catchError, finalize, of, takeUntil } from 'rxjs';
import {
  AdminPayrollOpsPanel,
  PAYROLL_OPS_ERROR_KEY_MAP,
  PAYROLL_OPS_FALLBACK_ERROR_KEY,
} from './admin-payroll-ops.models';

function parseAmount(value: string): number | null {
  const trimmed = value.trim();
  if (!trimmed) {
    return null;
  }
  const parsed = Number(trimmed);
  return Number.isFinite(parsed) && parsed >= 0 ? parsed : null;
}

@Injectable()
export class AdminPayrollOpsFacade extends UnsubscribeControlDirective {
  private readonly adminClient = inject(AdminClient);
  private readonly snackbar = inject(SnackbarService);
  private readonly translate = inject(TranslateService);

  readonly activePanel = signal<AdminPayrollOpsPanel | null>(null);
  readonly submitting = signal<boolean>(false);
  readonly errorKey = signal<string | null>(null);

  readonly bonusAmount = signal<string>('');
  readonly deductionAmount = signal<string>('');
  readonly adjustNotes = signal<string>('');
  readonly disputeNotes = signal<string>('');
  readonly rejectNotes = signal<string>('');

  readonly canSubmitAdjust = computed(
    () =>
      parseAmount(this.bonusAmount()) !== null &&
      parseAmount(this.deductionAmount()) !== null &&
      !this.submitting()
  );
  readonly canSubmitDispute = computed(
    () => this.disputeNotes().trim().length > 0 && !this.submitting()
  );
  readonly canSubmitReject = computed(
    () => this.rejectNotes().trim().length > 0 && !this.submitting()
  );

  openPanel(panel: AdminPayrollOpsPanel): void {
    if (this.activePanel() === panel) {
      this.closePanel();
      return;
    }
    this.resetInputs();
    this.activePanel.set(panel);
  }

  openAdjustPanel(currentBonus: number, currentDeduction: number): void {
    if (this.activePanel() === 'adjust') {
      this.closePanel();
      return;
    }
    this.resetInputs();
    this.bonusAmount.set(String(currentBonus));
    this.deductionAmount.set(String(currentDeduction));
    this.activePanel.set('adjust');
  }

  closePanel(): void {
    this.activePanel.set(null);
    this.resetInputs();
  }

  setBonusAmount(value: string): void {
    this.bonusAmount.set(value);
  }

  setDeductionAmount(value: string): void {
    this.deductionAmount.set(value);
  }

  setAdjustNotes(value: string): void {
    this.adjustNotes.set(value);
  }

  setDisputeNotes(value: string): void {
    this.disputeNotes.set(value);
  }

  setRejectNotes(value: string): void {
    this.rejectNotes.set(value);
  }

  adjustAmounts(invoiceId: string, onSuccess: () => void): void {
    const bonusAmount = parseAmount(this.bonusAmount());
    const deductionAmount = parseAmount(this.deductionAmount());
    if (!invoiceId || bonusAmount === null || deductionAmount === null) {
      return;
    }
    const command = new UpdateInvoiceAmountsCommand({
      invoiceId,
      bonusAmount,
      deductionAmount,
      adminNotes: this.adjustNotes().trim() || undefined,
    });
    this.run(
      this.adminClient.adminPayrollClient.updateInvoiceAmounts(command),
      'pages.invoice_detail.ops.adjust.success',
      onSuccess
    );
  }

  disputeInvoice(invoiceId: string, onSuccess: () => void): void {
    const adminNotes = this.disputeNotes().trim();
    if (!invoiceId || !adminNotes) {
      return;
    }
    const command = new DisputeInvoiceCommand({ invoiceId, adminNotes });
    this.run(
      this.adminClient.adminPayrollClient.disputeInvoice(command),
      'pages.invoice_detail.ops.dispute.success',
      onSuccess
    );
  }

  rejectInvoice(invoiceId: string, onSuccess: () => void): void {
    const adminNotes = this.rejectNotes().trim();
    if (!invoiceId || !adminNotes) {
      return;
    }
    const command = new RejectInvoiceCommand({ invoiceId, adminNotes });
    this.run(
      this.adminClient.adminPayrollClient.rejectInvoice(command),
      'pages.invoice_detail.ops.reject.success',
      onSuccess
    );
  }

  private run(
    request$: Observable<
      | UpdateInvoiceAmountsResponse
      | DisputeInvoiceResponse
      | RejectInvoiceResponse
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
              this.errorKey() ?? PAYROLL_OPS_FALLBACK_ERROR_KEY
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
    this.bonusAmount.set('');
    this.deductionAmount.set('');
    this.adjustNotes.set('');
    this.disputeNotes.set('');
    this.rejectNotes.set('');
    this.errorKey.set(null);
  }

  private resolveErrorKey(error: unknown): string {
    const code = extractApiErrorCode(error);
    if (code && PAYROLL_OPS_ERROR_KEY_MAP[code]) {
      return PAYROLL_OPS_ERROR_KEY_MAP[code];
    }
    return PAYROLL_OPS_FALLBACK_ERROR_KEY;
  }
}
