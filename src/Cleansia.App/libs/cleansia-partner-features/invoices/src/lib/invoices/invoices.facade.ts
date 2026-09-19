import { Injectable, inject, signal } from '@angular/core';
import { AbstractControl } from '@angular/forms';
import { UnsubscribeControlDirective } from '@cleansia/directives';
import {
  EmployeeInvoiceDto,
  PagedDataOfEmployeeInvoiceDto,
  EmployeeInvoiceStatus,
  PartnerClient,
  SortDefinition,
} from '@cleansia/partner-services';
import { SnackbarService } from '@cleansia/services';
import { TranslateService } from '@ngx-translate/core';
import { catchError, debounceTime, distinctUntilChanged, of, takeUntil } from 'rxjs';

@Injectable()
export class InvoicesFacade extends UnsubscribeControlDirective {
  private readonly snackbarService = inject(SnackbarService);
  private readonly partnerClient = inject(PartnerClient);
  private readonly translate = inject(TranslateService);

  invoices = signal<EmployeeInvoiceDto[]>([]);
  loading = signal<boolean>(false);
  totalRecords = signal<number>(0);

  private currentEmployeeId = signal<string | null>(null);
  private currentSort = signal<SortDefinition[]>([]);
  private currentFilter = signal<{
    invoiceNumber?: string;
    minAmount?: number;
    maxAmount?: number;
    dateFrom?: Date;
    dateTo?: Date;
    payPeriodId?: string;
    statuses?: EmployeeInvoiceStatus[];
  } | null>(null);

  constructor() {
    super();
    // Get current employee ID
    this.loadCurrentEmployee();
  }

  private loadCurrentEmployee(): void {
    this.partnerClient.employeeClient
      .getCurrentEmployee()
      .pipe(
        takeUntil(this.destroyed$),
        catchError(() => of(null))
      )
      .subscribe((employee) => {
        if (employee?.id) {
          this.currentEmployeeId.set(employee.id);
          this.loadInvoices();
        }
      });
  }

  loadInvoices(offset = 0, limit = 20): void {
    const employeeId = this.currentEmployeeId();

    if (!employeeId) {
      return;
    }

    this.loading.set(true);

    const filter = this.currentFilter();

    this.partnerClient.employeePayrollClient
      .getPagedInvoices(
        employeeId,
        filter?.payPeriodId,
        filter?.statuses,
        filter?.invoiceNumber,
        filter?.minAmount,
        filter?.maxAmount,
        filter?.dateFrom,
        filter?.dateTo,
        undefined, // currencyId: one invoice per currency, listed together
        this.currentSort(),
        offset,
        limit
      )
      .pipe(
        takeUntil(this.destroyed$),
        catchError(() => {
          this.loading.set(false);
          return of(null);
        })
      )
      .subscribe((pagedData: PagedDataOfEmployeeInvoiceDto | null) => {
        if (pagedData && pagedData.data) {
          this.invoices.set(pagedData.data);
          this.totalRecords.set(pagedData.total ?? 0);
        } else {
          this.invoices.set([]);
          this.totalRecords.set(0);
        }
        this.loading.set(false);
      });
  }

  updateSort(sort: SortDefinition[]): void {
    this.currentSort.set(sort);
    // Reset to first page when sorting changes
    this.loadInvoices(0, 10);
  }

  applyFilters(filter: {
    invoiceNumber?: string;
    minAmount?: number;
    maxAmount?: number;
    dateFrom?: Date;
    dateTo?: Date;
    payPeriodId?: string;
    statuses?: EmployeeInvoiceStatus[];
  }): void {
    this.currentFilter.set(filter);
    // Reset to first page when filters change
    this.loadInvoices(0, 10);
  }

  resetFilters(): void {
    this.currentFilter.set(null);
    // Reset to first page when filters are cleared
    this.loadInvoices(0, 10);
  }

  downloadInvoice(invoice: EmployeeInvoiceDto): void {
    if (!invoice.id || !invoice.pdfBlobName) {
      this.snackbarService.showErrorTranslated(
        'pages.invoices.pdf_not_available'
      );
      return;
    }

    this.partnerClient.employeePayrollClient
      .downloadInvoice(invoice.id)
      .pipe(
        takeUntil(this.destroyed$),
        catchError(() => of(null))
      )
      .subscribe((fileResponse) => {
        if (fileResponse) {
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
        }
      });
  }

  /**
   * Wire form valueChanges and language change subscriptions.
   * Component lifecycle invokes this once; cleanup is handled by the
   * facade's destroyed$ Subject (UnsubscribeControlDirective).
   */
  bindFormChanges(
    formCtrl: AbstractControl,
    onFormChangeImmediate: () => void,
    onFormChangeDebounced: () => void,
    onLangChange: () => void
  ): void {
    formCtrl.valueChanges
      .pipe(takeUntil(this.destroyed$))
      .subscribe(() => onFormChangeImmediate());

    formCtrl.valueChanges
      .pipe(debounceTime(500), distinctUntilChanged(), takeUntil(this.destroyed$))
      .subscribe(() => onFormChangeDebounced());

    this.translate.onLangChange
      .pipe(takeUntil(this.destroyed$))
      .subscribe(() => onLangChange());
  }
}
