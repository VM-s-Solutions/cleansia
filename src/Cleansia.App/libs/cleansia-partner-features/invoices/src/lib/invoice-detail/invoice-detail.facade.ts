import { Injectable, inject, signal } from '@angular/core';
import {
  EmployeeInvoiceDetailDto,
  PartnerClient,
} from '@cleansia/partner-services';
import { SnackbarService } from '@cleansia/services';
import { catchError, finalize, of, tap } from 'rxjs';

@Injectable()
export class InvoiceDetailFacade {
  private readonly partnerClient = inject(PartnerClient);
  private readonly snackbarService = inject(SnackbarService);

  // Signals for reactive state management
  readonly invoiceDetail = signal<EmployeeInvoiceDetailDto | null>(null);
  readonly loading = signal(false);
  readonly error = signal<string | null>(null);

  loadInvoiceDetail(invoiceId: string): void {
    if (!invoiceId?.trim()) {
      this.error.set('Invalid invoice ID');
      return;
    }

    this.loading.set(true);
    this.error.set(null);

    this.partnerClient.employeePayrollClient
      .getInvoiceById(invoiceId)
      .pipe(
        tap((invoiceDetail) => {
          if (invoiceDetail) {
            this.invoiceDetail.set(invoiceDetail);
          } else {
            this.error.set('Invoice not found');
          }
        }),
        catchError((error) => {
          console.error('Error loading invoice details:', error);
          const errorMessage =
            error?.status === 404
              ? 'Invoice not found'
              : 'Failed to load invoice details';
          this.error.set(errorMessage);
          this.snackbarService.showErrorTranslated(
            'global.messages.invoices.failed_to_load_details'
          );
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

    // Download PDF from server
    this.partnerClient.employeePayrollClient
      .downloadInvoice(invoice.id!)
      .pipe(
        catchError((error) => {
          console.error('Failed to download invoice:', error);
          this.snackbarService.showErrorTranslated(
            'pages.invoices.download_failed'
          );
          return of(null);
        })
      )
      .subscribe((fileResponse) => {
        if (fileResponse) {
          // Create a blob from the file data and trigger download
          const blob = fileResponse.data;
          const url = window.URL.createObjectURL(blob);
          const link = document.createElement('a');
          link.href = url;
          link.download =
            fileResponse.fileName || `invoice-${invoice.invoiceNumber}.pdf`;
          link.click();
          window.URL.revokeObjectURL(url);

          this.snackbarService.showSuccessTranslated(
            'global.messages.invoices.invoice_downloaded'
          );
        }
      });
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
