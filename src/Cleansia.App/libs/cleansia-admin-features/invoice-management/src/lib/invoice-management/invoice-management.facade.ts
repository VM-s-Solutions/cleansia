import { computed, Injectable, inject, signal } from '@angular/core';
import { FormBuilder } from '@angular/forms';
import {
  AdminClient,
  EmployeeInvoiceDto,
  EmployeeInvoiceStatus,
  RegenerateInvoicePdfCommand,
  SortDefinition,
  SortDirection,
} from '@cleansia/admin-services';
import { FilterChip, FilterDrawerState, PaginationState, SortEvent } from '@cleansia/components';
import { UnsubscribeControlDirective } from '@cleansia/directives';
import { SnackbarService, extractApiErrorCode } from '@cleansia/services';
import { currentLanguage } from '@cleansia/utils';
import { TranslateService } from '@ngx-translate/core';
import { catchError, finalize, of, takeUntil } from 'rxjs';
import {
  RETRY_PDF_ERROR_KEY_MAP,
  RETRY_PDF_FALLBACK_ERROR_KEY,
} from './invoice-management.models';

export interface InvoiceFilterParams {
  statuses?: EmployeeInvoiceStatus[];
  employeeId?: string;
  payPeriodId?: string;
  currencyId?: string;
}

@Injectable()
export class InvoiceManagementFacade extends UnsubscribeControlDirective {
  private readonly adminClient = inject(AdminClient);
  private readonly snackbarService = inject(SnackbarService);
  private readonly translate = inject(TranslateService);

  readonly invoices = signal<EmployeeInvoiceDto[]>([]);
  readonly loading = signal<boolean>(false);
  readonly initialLoading = signal<boolean>(true);
  readonly totalRecords = signal<number>(0);
  readonly retryingPdf = signal<boolean>(false);

  readonly lang = currentLanguage(this.translate);
  readonly invoiceStatusOptions = computed(() => {
    this.lang();
    return [
      EmployeeInvoiceStatus.Pending,
      EmployeeInvoiceStatus.Approved,
      EmployeeInvoiceStatus.Paid,
      EmployeeInvoiceStatus.Disputed,
      EmployeeInvoiceStatus.Rejected,
      EmployeeInvoiceStatus.Cancelled,
    ].map((status) => ({
      label: this.translate.instant(
        `pages.invoice_management.invoice_status.${EmployeeInvoiceStatus[status].toLowerCase()}`
      ),
      value: status,
    }));
  });
  readonly filterForm = inject(FormBuilder).group({
    status: [[] as EmployeeInvoiceStatus[]],
    currencyId: [null as string | null],
  });
  readonly filters = new FilterDrawerState({
    form: this.filterForm,
    lang: this.lang,
    chips: (value): FilterChip[] => [
      ...(value.status?.length
        ? [
            {
              key: 'status',
              label: this.translate.instant('pages.invoice_management.filters.status'),
              value: value.status
                .map((s) => this.invoiceStatusOptions().find((o) => o.value === s)?.label)
                .filter(Boolean)
                .join(', '),
            },
          ]
        : []),
      ...(value.currencyId
        ? [
            {
              key: 'currencyId',
              label: this.translate.instant('pages.invoice_management.filters.currency'),
              value: this.currencies().find((c) => c.id === value.currencyId)?.code ?? '',
            },
          ]
        : []),
    ],
    apply: (value) =>
      this.applyFilter({
        statuses: value.status?.length ? value.status : undefined,
        currencyId: value.currencyId || undefined,
      }),
  });

  private currentFilter = signal<InvoiceFilterParams | null>(null);
  private currentOffset = signal<number>(0);
  private currentLimit = signal<number>(20);
  private currentSort = signal<SortDefinition[] | undefined>(undefined);

  readonly currencies = signal<{ id: string; code: string; isDefault: boolean }[]>([]);

  constructor() {
    super();
    this.filters.connect(this.destroyed$);
  }

  loadCurrencies(): void {
    this.adminClient.adminCurrencyClient
      .getOverview()
      .pipe(takeUntil(this.destroyed$), catchError(() => of([])))
      .subscribe((currencies) => {
        this.currencies.set(
          (currencies ?? []).flatMap((c) =>
            c.id && c.code ? [{ id: c.id, code: c.code, isDefault: !!c.isDefault }] : []
          )
        );
      });
  }

  loadInvoices(): void {
    this.loading.set(true);
    const filterParams = this.currentFilter();

    this.adminClient.adminInvoiceClient
      .getPaged(
        filterParams?.employeeId,
        filterParams?.payPeriodId,
        filterParams?.statuses,
        undefined, // invoiceNumber
        undefined, // minAmount
        undefined, // maxAmount
        undefined, // dateFrom
        undefined, // dateTo
        filterParams?.currencyId, // currencyId
        this.currentSort(),
        this.currentOffset(),
        this.currentLimit()
      )
      .pipe(
        takeUntil(this.destroyed$),
        catchError(() => of(null)),
        finalize(() => this.loading.set(false))
      )
      .subscribe((response) => {
        if (response) {
          this.invoices.set(response.data || []);
          this.totalRecords.set(response.total || 0);
        }
        if (this.initialLoading()) {
          this.initialLoading.set(false);
        }
      });
  }

  onPageChange(event: PaginationState): void {
    this.currentOffset.set(event.first);
    this.currentLimit.set(event.rows);
    this.loadInvoices();
  }

  onSortChange(event: SortEvent): void {
    this.currentSort.set([
      new SortDefinition({
        field: event.field,
        direction: event.order === 1 ? SortDirection.Ascending : SortDirection.Descending,
      }),
    ]);
    this.loadInvoices();
  }

  applyFilter(filter: InvoiceFilterParams): void {
    this.currentFilter.set(filter);
    this.currentOffset.set(0);
    this.loadInvoices();
  }

  isStatusChecked(status: EmployeeInvoiceStatus): boolean {
    return this.filterForm.value.status?.includes(status) ?? false;
  }

  setStatus(status: EmployeeInvoiceStatus, checked: boolean): void {
    const current = this.filterForm.value.status || [];
    this.filterForm.patchValue({
      status: checked ? [...new Set([...current, status])] : current.filter((s) => s !== status),
    });
  }

  downloadInvoice(invoice: EmployeeInvoiceDto): void {
    if (!invoice.id) return;

    this.adminClient.adminInvoiceClient
      .download(invoice.id)
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
            response.fileName || `invoice-${invoice.invoiceNumber}.pdf`;
          link.click();
          window.URL.revokeObjectURL(url);
        }
      });
  }

  retryPdf(invoice: EmployeeInvoiceDto): void {
    if (!invoice.id) return;

    this.retryingPdf.set(true);

    const command = new RegenerateInvoicePdfCommand();
    command.invoiceId = invoice.id;
    command.languageCode = this.translate.currentLang || 'en';

    this.adminClient.adminInvoiceClient
      .regeneratePdf(command)
      .pipe(
        takeUntil(this.destroyed$),
        catchError((error: unknown) => {
          this.snackbarService.showError(
            this.translate.instant(this.resolveRetryErrorKey(error))
          );
          return of(null);
        }),
        finalize(() => this.retryingPdf.set(false))
      )
      .subscribe((response) => {
        if (response) {
          this.snackbarService.showSuccess(
            this.translate.instant(
              'pages.invoice_management.messages.retry_pdf_success'
            )
          );
          this.loadInvoices();
        }
      });
  }

  private resolveRetryErrorKey(error: unknown): string {
    const code = extractApiErrorCode(error);
    if (code && RETRY_PDF_ERROR_KEY_MAP[code]) {
      return RETRY_PDF_ERROR_KEY_MAP[code];
    }
    return RETRY_PDF_FALLBACK_ERROR_KEY;
  }
}
