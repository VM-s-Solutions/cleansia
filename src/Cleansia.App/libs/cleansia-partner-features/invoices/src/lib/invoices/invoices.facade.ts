import { computed, Injectable, inject, signal } from '@angular/core';
import { FormBuilder } from '@angular/forms';
import { FilterChip, FilterDrawerState, PaginationState, SortEvent } from '@cleansia/components';
import { UnsubscribeControlDirective } from '@cleansia/directives';
import {
  EmployeeInvoiceDto,
  PagedDataOfEmployeeInvoiceDto,
  EmployeeInvoiceStatus,
  PartnerClient,
  SortDefinition,
  SortDirection,
} from '@cleansia/partner-services';
import { SnackbarService } from '@cleansia/services';
import { currentLanguage } from '@cleansia/utils';
import { TranslateService } from '@ngx-translate/core';
import { catchError, of, takeUntil } from 'rxjs';
import { buildFilterChips, buildInvoiceStatusOptions } from './invoices.helpers';

@Injectable()
export class InvoicesFacade extends UnsubscribeControlDirective {
  private readonly snackbarService = inject(SnackbarService);
  private readonly partnerClient = inject(PartnerClient);
  private readonly translate = inject(TranslateService);

  invoices = signal<EmployeeInvoiceDto[]>([]);
  loading = signal<boolean>(false);
  totalRecords = signal<number>(0);

  readonly lang = currentLanguage(this.translate);
  readonly invoiceStatusOptions = computed(() => {
    this.lang();
    return buildInvoiceStatusOptions(this.translate);
  });
  // One boolean control per status feeds the `statuses` array the query sends, so a new status
  // needs no matching FormControl declared by hand.
  readonly filterForm = inject(FormBuilder).group({
    invoiceNumber: [''],
    minAmount: [null as number | null],
    maxAmount: [null as number | null],
    dateFrom: [null as Date | null],
    dateTo: [null as Date | null],
    statuses: [[] as number[]],
    ...Object.fromEntries(
      buildInvoiceStatusOptions(this.translate).map((option) => [`status_${option.value}`, [false]])
    ),
  });
  readonly filters = new FilterDrawerState({
    form: this.filterForm,
    lang: this.lang,
    chips: (value): FilterChip[] => buildFilterChips(value, this.invoiceStatusOptions(), this.translate),
    apply: (value) =>
      this.applyFilters({
        invoiceNumber: value.invoiceNumber || undefined,
        minAmount: value.minAmount || undefined,
        maxAmount: value.maxAmount || undefined,
        dateFrom: value.dateFrom || undefined,
        dateTo: value.dateTo || undefined,
        statuses: value.statuses?.length ? value.statuses : undefined,
      }),
  });

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
    this.filters.connect(this.destroyed$);
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

  onPageChange(event: PaginationState): void {
    this.loadInvoices(event.first, event.rows);
  }

  onSortChange(event: SortEvent): void {
    this.currentSort.set([
      new SortDefinition({
        field: event.field,
        direction: event.order === 1 ? SortDirection.Ascending : SortDirection.Descending,
      }),
    ]);
    // Reset to first page when sorting changes
    this.loadInvoices();
  }

  setInvoiceStatus(status: number, checked: boolean): void {
    const current = this.filterForm.controls.statuses.value || [];
    this.filterForm.patchValue({
      statuses: checked ? [...new Set([...current, status])] : current.filter((s) => s !== status),
    });
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
    this.loadInvoices();
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
}
