import { Injectable, inject, signal } from '@angular/core';
import { UnsubscribeControlDirective } from '@cleansia/directives';
import {
  EmployeeInvoiceDetailDto,
  PartnerClient,
} from '@cleansia/partner-services';
import { SnackbarService } from '@cleansia/services';
import { TranslateService } from '@ngx-translate/core';
import { catchError, finalize, of, tap, takeUntil } from 'rxjs';

@Injectable()
export class InvoiceDetailFacade extends UnsubscribeControlDirective {
  private readonly partnerClient = inject(PartnerClient);
  private readonly snackbarService = inject(SnackbarService);
  private readonly translateService = inject(TranslateService);

  // Signals for reactive state management
  readonly invoiceDetail = signal<EmployeeInvoiceDetailDto | null>(null);
  readonly loading = signal(false);
  readonly error = signal<string | null>(null);

  loadInvoiceDetail(invoiceId: string): void {
    if (!invoiceId?.trim()) {
      this.error.set(this.translateService.instant('pages.invoice_detail.not_found_message'));
      return;
    }

    this.loading.set(true);
    this.error.set(null);

    this.partnerClient.employeePayrollClient
      .getInvoiceById(invoiceId)
      .pipe(
        takeUntil(this.destroyed$),
        tap((invoiceDetail) => {
          if (invoiceDetail) {
            this.invoiceDetail.set(invoiceDetail);
          } else {
            this.error.set(this.translateService.instant('pages.invoice_detail.not_found_message'));
          }
        }),
        catchError((error) => {
          const errorMessage = error?.status === 404
            ? this.translateService.instant('pages.invoice_detail.not_found_message')
            : this.translateService.instant('pages.invoice_detail.load_failed');
          this.error.set(errorMessage);
          return of(null);
        }),
        finalize(() => this.loading.set(false))
      )
      .subscribe();
  }

  downloadPdf(): void {
    const invoice = this.invoiceDetail();
    if (!invoice) {
      this.snackbarService.showErrorTranslated(
        'global.messages.invoices.no_invoice_selected'
      );
      return;
    }

    if (!invoice.pdfBlobName) {
      this.snackbarService.showErrorTranslated(
        'pages.invoices.pdf_not_available'
      );
      return;
    }

    this.loading.set(true);

    this.partnerClient.employeePayrollClient
      .downloadInvoice(invoice.id!)
      .pipe(
        takeUntil(this.destroyed$),
        tap((fileResponse) => {
          const blob = fileResponse.data;
          const url = window.URL.createObjectURL(blob);
          const link = document.createElement('a');
          link.href = url;
          link.download =
            fileResponse.fileName || `invoice-${invoice.invoiceNumber}.pdf`;
          document.body.appendChild(link);
          link.click();
          document.body.removeChild(link);
          window.URL.revokeObjectURL(url);

          this.snackbarService.showSuccessTranslated(
            'global.messages.invoices.invoice_downloaded'
          );
        }),
        catchError(() => of(null)),
        finalize(() => this.loading.set(false))
      )
      .subscribe();
  }

  printInvoice(): void {
    try {
      window.print();
    } catch (error) {
      console.error('Print failed:', error);
      this.snackbarService.showErrorTranslated(
        'global.messages.invoices.print_failed'
      );
    }
  }

  reset(): void {
    this.invoiceDetail.set(null);
    this.loading.set(false);
    this.error.set(null);
  }
}
