import { Injectable, inject, signal } from '@angular/core';
import {
  AdminClient,
  ApproveInvoiceCommand,
  AssignInvoiceVariableSymbolCommand,
  CancelInvoiceCommand,
  EmployeeInvoiceDetailDto,
  EmployeeInvoiceStatus,
  MarkInvoicePaidCommand,
  RegenerateInvoicePdfCommand,
} from '@cleansia/admin-services';
import { UnsubscribeControlDirective } from '@cleansia/directives';
import { SnackbarService } from '@cleansia/services';
import { TranslateService } from '@ngx-translate/core';
import { DialogService } from 'primeng/dynamicdialog';
import { catchError, finalize, of, takeUntil } from 'rxjs';
import {
  RejectDialogComponent,
  RejectDialogData,
  RejectDialogResult,
} from '../../../../employee-management/src/lib/components';

@Injectable()
export class InvoiceDetailFacade extends UnsubscribeControlDirective {
  private readonly adminClient = inject(AdminClient);
  private readonly dialogService = inject(DialogService);
  private readonly snackbarService = inject(SnackbarService);
  private readonly translate = inject(TranslateService);

  readonly invoice = signal<EmployeeInvoiceDetailDto | null>(null);
  readonly loading = signal<boolean>(false);
  readonly actionLoading = signal<boolean>(false);

  loadInvoiceDetail(invoiceId: string): void {
    this.loading.set(true);

    this.adminClient.adminInvoiceClient
      .details(invoiceId)
      .pipe(
        takeUntil(this.destroyed$),
        catchError(() => of(null)),
        finalize(() => this.loading.set(false))
      )
      .subscribe((response) => {
        if (response) {
          this.invoice.set(response);
        }
      });
  }

  approveInvoice(): void {
    const inv = this.invoice();
    if (!inv?.id) return;

    this.actionLoading.set(true);

    const command = new ApproveInvoiceCommand();
    command.invoiceId = inv.id;
    command.adminNotes = undefined;

    this.adminClient.adminInvoiceClient
      .approve(command)
      .pipe(
        takeUntil(this.destroyed$),
        catchError(() => of(null)),
        finalize(() => this.actionLoading.set(false))
      )
      .subscribe((response) => {
        if (response) {
          this.snackbarService.showSuccess(
            this.translate.instant(
              'pages.invoice_detail.messages.approve_success'
            )
          );
          this.loadInvoiceDetail(inv.id!);
        }
      });
  }

  markAsPaid(bankTransferNote?: string): void {
    const inv = this.invoice();
    if (!inv?.id) return;

    this.actionLoading.set(true);

    const command = new MarkInvoicePaidCommand();
    command.invoiceId = inv.id;
    command.bankTransferNote = bankTransferNote;
    command.adminNotes = undefined;

    this.adminClient.adminInvoiceClient
      .markPaid(command)
      .pipe(
        takeUntil(this.destroyed$),
        catchError(() => of(null)),
        finalize(() => this.actionLoading.set(false))
      )
      .subscribe((response) => {
        if (response) {
          this.snackbarService.showSuccess(
            this.translate.instant(
              'pages.invoice_detail.messages.mark_paid_success'
            )
          );
          this.loadInvoiceDetail(inv.id!);
        }
      });
  }

  assignVariableSymbol(): void {
    const inv = this.invoice();
    if (!inv?.id) return;

    this.actionLoading.set(true);

    const command = new AssignInvoiceVariableSymbolCommand();
    command.invoiceId = inv.id;
    command.languageCode = this.translate.currentLang || 'en';

    this.adminClient.adminPayrollClient
      .assignInvoiceVariableSymbol(command)
      .pipe(
        takeUntil(this.destroyed$),
        catchError(() => of(null)),
        finalize(() => this.actionLoading.set(false))
      )
      .subscribe((response) => {
        if (!response) return;

        const variableSymbol = response.variableSymbol;

        // The reference is committed before the PDF is re-rendered, so an absent
        // blob url means a durable number on a document that does not print it —
        // the one outcome that must not read as an unqualified success.
        if (response.pdfBlobUrl) {
          this.snackbarService.showSuccess(
            this.translate.instant(
              'pages.invoice_detail.messages.assign_variable_symbol_success',
              { variableSymbol }
            )
          );
        } else {
          this.snackbarService.showError(
            this.translate.instant(
              'pages.invoice_detail.messages.assign_variable_symbol_pdf_stale',
              { variableSymbol }
            )
          );
        }

        this.loadInvoiceDetail(inv.id!);
      });
  }

  openCancelDialog(): void {
    const dialogData: RejectDialogData = {
      title: this.translate.instant('pages.invoice_detail.cancel_dialog.title'),
      subtitle: this.translate.instant(
        'pages.invoice_detail.cancel_dialog.subtitle'
      ),
      reasonLabel: this.translate.instant(
        'pages.invoice_detail.cancel_dialog.reason_label'
      ),
      reasonPlaceholder: this.translate.instant(
        'pages.invoice_detail.cancel_dialog.reason_placeholder'
      ),
    };

    const dialogRef = this.dialogService.open(RejectDialogComponent, {
      data: dialogData,
      header: this.translate.instant(
        'pages.invoice_detail.cancel_dialog.title'
      ),
      width: '500px',
      modal: true,
    });

    dialogRef.onClose.pipe(takeUntil(this.destroyed$)).subscribe((result: RejectDialogResult | undefined) => {
      if (result?.reason) {
        this.cancelInvoice(result.reason);
      }
    });
  }

  cancelInvoice(reason: string): void {
    const inv = this.invoice();
    if (!inv?.id) return;

    this.actionLoading.set(true);

    const command = new CancelInvoiceCommand();
    command.invoiceId = inv.id;
    command.reason = reason;

    this.adminClient.adminInvoiceClient
      .cancel(command)
      .pipe(
        takeUntil(this.destroyed$),
        catchError(() => of(null)),
        finalize(() => this.actionLoading.set(false))
      )
      .subscribe((response) => {
        if (response) {
          this.snackbarService.showSuccess(
            this.translate.instant(
              'pages.invoice_detail.messages.cancel_success'
            )
          );
          this.loadInvoiceDetail(inv.id!);
        }
      });
  }

  downloadInvoice(): void {
    const inv = this.invoice();
    if (!inv?.id) return;

    this.adminClient.adminInvoiceClient
      .download(inv.id)
      .pipe(
        takeUntil(this.destroyed$),
        catchError(() => of(null))
      )
      .subscribe((response) => {
        if (response && response.data) {
          const blob = response.data;
          const url = window.URL.createObjectURL(blob);
          const link = document.createElement('a');
          link.href = url;
          link.download =
            response.fileName || `invoice-${inv.invoiceNumber}.pdf`;
          link.click();
          window.URL.revokeObjectURL(url);
        }
      });
  }

  regeneratePdf(): void {
    const inv = this.invoice();
    if (!inv?.id) return;

    this.actionLoading.set(true);

    const command = new RegenerateInvoicePdfCommand();
    command.invoiceId = inv.id;
    command.languageCode = this.translate.currentLang || 'en';

    this.adminClient.adminInvoiceClient
      .regeneratePdf(command)
      .pipe(
        takeUntil(this.destroyed$),
        catchError(() => of(null)),
        finalize(() => this.actionLoading.set(false))
      )
      .subscribe((response) => {
        if (response) {
          this.snackbarService.showSuccess(
            this.translate.instant(
              'pages.invoice_detail.messages.regenerate_success'
            )
          );
          this.loadInvoiceDetail(inv.id!);
        }
      });
  }

  getStatusLabel(status: EmployeeInvoiceStatus | undefined): string {
    if (!status) return '';
    switch (status) {
      case EmployeeInvoiceStatus.Pending:
        return this.translate.instant(
          'pages.invoice_detail.invoice_status.pending'
        );
      case EmployeeInvoiceStatus.Approved:
        return this.translate.instant(
          'pages.invoice_detail.invoice_status.approved'
        );
      case EmployeeInvoiceStatus.Paid:
        return this.translate.instant(
          'pages.invoice_detail.invoice_status.paid'
        );
      case EmployeeInvoiceStatus.Disputed:
        return this.translate.instant(
          'pages.invoice_detail.invoice_status.disputed'
        );
      case EmployeeInvoiceStatus.Rejected:
        return this.translate.instant(
          'pages.invoice_detail.invoice_status.rejected'
        );
      case EmployeeInvoiceStatus.Cancelled:
        return this.translate.instant(
          'pages.invoice_detail.invoice_status.cancelled'
        );
      default:
        return '';
    }
  }

  formatDate(date: string | Date | null | undefined): string {
    if (!date) return '-';
    const dateObj = date instanceof Date ? date : new Date(date);
    return dateObj.toLocaleDateString('en-GB');
  }

  formatDateTime(date: string | Date | null | undefined): string {
    if (!date) return '-';
    const dateObj = date instanceof Date ? date : new Date(date);
    return dateObj.toLocaleString('en-GB');
  }

  formatCurrency(
    amount: number | null | undefined,
    currencyCode?: string
  ): string {
    if (amount === null || amount === undefined) return '-';
    const currency = currencyCode || 'CZK';
    return `${amount.toFixed(2)} ${currency}`;
  }

  canApprove(): boolean {
    const status = this.invoice()?.status;
    return status === EmployeeInvoiceStatus.Pending;
  }

  canMarkPaid(): boolean {
    const status = this.invoice()?.status;
    return status === EmployeeInvoiceStatus.Approved;
  }

  canCancel(): boolean {
    const status = this.invoice()?.status;
    return (
      status === EmployeeInvoiceStatus.Pending ||
      status === EmployeeInvoiceStatus.Approved
    );
  }

  canDownload(): boolean {
    return !!this.invoice()?.pdfBlobName;
  }

  canAssignVariableSymbol(): boolean {
    const inv = this.invoice();
    if (!inv || inv.variableSymbol) return false;

    return (
      inv.status === EmployeeInvoiceStatus.Pending ||
      inv.status === EmployeeInvoiceStatus.Approved ||
      inv.status === EmployeeInvoiceStatus.Disputed
    );
  }
}
