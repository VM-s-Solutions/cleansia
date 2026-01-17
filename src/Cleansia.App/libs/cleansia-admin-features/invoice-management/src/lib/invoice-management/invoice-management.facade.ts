import { Injectable, inject, signal } from '@angular/core';
import {
  AdminClient,
  EmployeeInvoiceDto,
  EmployeeInvoiceStatus,
  SortDefinition,
} from '@cleansia/admin-services';
import { SnackbarService } from '@cleansia/services';
import { TranslateService } from '@ngx-translate/core';
import { Subject, catchError, finalize, of, takeUntil } from 'rxjs';

export interface InvoiceFilterParams {
  statuses?: EmployeeInvoiceStatus[];
  employeeId?: string;
  payPeriodId?: string;
}

@Injectable()
export class InvoiceManagementFacade {
  private readonly adminClient = inject(AdminClient);
  private readonly snackbarService = inject(SnackbarService);
  private readonly translate = inject(TranslateService);

  private destroy$ = new Subject<void>();

  readonly invoices = signal<EmployeeInvoiceDto[]>([]);
  readonly loading = signal<boolean>(false);
  readonly initialLoading = signal<boolean>(true);
  readonly totalRecords = signal<number>(0);

  private currentFilter = signal<InvoiceFilterParams | null>(null);
  private currentOffset = signal<number>(0);
  private currentLimit = signal<number>(20);
  private currentSort = signal<SortDefinition[] | undefined>(undefined);

  readonly invoiceStatusOptions = [
    {
      label: this.translate.instant(
        'pages.invoice_management.invoice_status.pending'
      ),
      value: EmployeeInvoiceStatus.Pending,
    },
    {
      label: this.translate.instant(
        'pages.invoice_management.invoice_status.approved'
      ),
      value: EmployeeInvoiceStatus.Approved,
    },
    {
      label: this.translate.instant(
        'pages.invoice_management.invoice_status.paid'
      ),
      value: EmployeeInvoiceStatus.Paid,
    },
    {
      label: this.translate.instant(
        'pages.invoice_management.invoice_status.disputed'
      ),
      value: EmployeeInvoiceStatus.Disputed,
    },
    {
      label: this.translate.instant(
        'pages.invoice_management.invoice_status.rejected'
      ),
      value: EmployeeInvoiceStatus.Rejected,
    },
    {
      label: this.translate.instant(
        'pages.invoice_management.invoice_status.cancelled'
      ),
      value: EmployeeInvoiceStatus.Cancelled,
    },
  ];

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
        this.currentSort(),
        this.currentOffset(),
        this.currentLimit()
      )
      .pipe(
        takeUntil(this.destroy$),
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

  onPageChange(offset: number, limit: number): void {
    this.currentOffset.set(offset);
    this.currentLimit.set(limit);
    this.loadInvoices();
  }

  onSortChange(sort: SortDefinition[] | undefined): void {
    this.currentSort.set(sort);
    this.loadInvoices();
  }

  applyFilter(filter: InvoiceFilterParams): void {
    this.currentFilter.set(filter);
    this.currentOffset.set(0);
    this.loadInvoices();
  }

  resetFilter(): void {
    this.currentFilter.set(null);
    this.currentOffset.set(0);
    this.loadInvoices();
  }

  downloadInvoice(invoice: EmployeeInvoiceDto): void {
    if (!invoice.id) return;

    this.adminClient.adminInvoiceClient
      .download(invoice.id)
      .pipe(
        takeUntil(this.destroy$),
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

  getStatusLabel(status: EmployeeInvoiceStatus | undefined): string {
    if (!status) return '';
    switch (status) {
      case EmployeeInvoiceStatus.Pending:
        return this.translate.instant(
          'pages.invoice_management.invoice_status.pending'
        );
      case EmployeeInvoiceStatus.Approved:
        return this.translate.instant(
          'pages.invoice_management.invoice_status.approved'
        );
      case EmployeeInvoiceStatus.Paid:
        return this.translate.instant(
          'pages.invoice_management.invoice_status.paid'
        );
      case EmployeeInvoiceStatus.Disputed:
        return this.translate.instant(
          'pages.invoice_management.invoice_status.disputed'
        );
      case EmployeeInvoiceStatus.Rejected:
        return this.translate.instant(
          'pages.invoice_management.invoice_status.rejected'
        );
      case EmployeeInvoiceStatus.Cancelled:
        return this.translate.instant(
          'pages.invoice_management.invoice_status.cancelled'
        );
      default:
        return '';
    }
  }

  ngOnDestroy(): void {
    this.destroy$.next();
    this.destroy$.complete();
  }
}
