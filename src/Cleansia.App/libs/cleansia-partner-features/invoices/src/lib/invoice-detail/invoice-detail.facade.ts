import { Injectable, inject, signal } from '@angular/core';
import { Client, EmployeeInvoiceDetailDto, SnackbarService } from '@cleansia/services';
import { catchError, finalize, of, tap } from 'rxjs';

@Injectable()
export class InvoiceDetailFacade {
  private readonly client = inject(Client);
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

    this.client.employeePayrollClient
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

    if (!invoice.pdfBlobUrl) {
      this.snackbarService.showErrorTranslated(
        'pages.invoices.pdf_not_available'
      );
      return;
    }

    // Open PDF in new tab
    window.open(invoice.pdfBlobUrl, '_blank');
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
