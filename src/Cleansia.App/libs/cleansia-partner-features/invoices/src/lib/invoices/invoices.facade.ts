import { Injectable, inject, signal } from '@angular/core';
import { UnsubscribeControlDirective } from '@cleansia/directives';
import {
  EmployeeInvoiceDto,
  PagedDataOfEmployeeInvoiceDto,
  EmployeeInvoiceStatus,
  PartnerClient,
  SortDefinition,
} from '@cleansia/partner-services';
import { SnackbarService } from '@cleansia/services';
import { catchError, of, takeUntil } from 'rxjs';

export interface EmployeeInvoice {
  id: string;
  employeeId: string;
  employeeName: string;
  payPeriodId: string;
  payPeriodLabel: string;
  invoiceNumber: string;
  variableSymbol: string;
  totalOrders: number;
  subTotal: number;
  bonusAmount: number;
  deductionAmount: number;
  totalAmount: number;
  currencyCode: string;
  status:
    | 'Pending'
    | 'Approved'
    | 'Paid'
    | 'Disputed'
    | 'Rejected'
    | 'Cancelled';
  pdfBlobName?: string;
  generatedAt: Date;
  approvedAt?: Date;
  approvedBy?: string;
  paidAt?: Date;
  adminNotes?: string;
  bankTransferNote?: string;
}

@Injectable()
export class InvoicesFacade extends UnsubscribeControlDirective {
  private readonly snackbarService = inject(SnackbarService);
  private readonly partnerClient = inject(PartnerClient);

  // Signals for reactive data
  invoices = signal<EmployeeInvoice[]>([]);
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

  private mapDtoToInvoice(dto: EmployeeInvoiceDto): EmployeeInvoice {
    return {
      id: dto.id!,
      employeeId: dto.employeeId!,
      employeeName: dto.employeeName!,
      payPeriodId: dto.payPeriodId!,
      payPeriodLabel: dto.payPeriodLabel!,
      invoiceNumber: dto.invoiceNumber!,
      variableSymbol: dto.variableSymbol!,
      totalOrders: dto.totalOrders!,
      subTotal: dto.subTotal!,
      bonusAmount: dto.bonusAmount!,
      deductionAmount: dto.deductionAmount!,
      totalAmount: dto.totalAmount!,
      currencyCode: dto.currencyCode!,
      status: this.mapStatusToString(dto.status!),
      pdfBlobName: dto.pdfBlobName ?? undefined,
      generatedAt: new Date(dto.generatedAt!),
      approvedAt: dto.approvedAt ? new Date(dto.approvedAt) : undefined,
      approvedBy: dto.approvedBy ?? undefined,
      paidAt: dto.paidAt ? new Date(dto.paidAt) : undefined,
      adminNotes: dto.adminNotes ?? undefined,
      bankTransferNote: dto.bankTransferNote ?? undefined,
    };
  }

  private mapStatusToString(
    status: EmployeeInvoiceStatus
  ): 'Pending' | 'Approved' | 'Paid' | 'Disputed' | 'Rejected' | 'Cancelled' {
    switch (status) {
      case EmployeeInvoiceStatus.Pending:
        return 'Pending';
      case EmployeeInvoiceStatus.Approved:
        return 'Approved';
      case EmployeeInvoiceStatus.Paid:
        return 'Paid';
      case EmployeeInvoiceStatus.Disputed:
        return 'Disputed';
      case EmployeeInvoiceStatus.Rejected:
        return 'Rejected';
      case EmployeeInvoiceStatus.Cancelled:
        return 'Cancelled';
      default:
        return 'Pending';
    }
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
          const invoices = pagedData.data.map((dto: EmployeeInvoiceDto) =>
            this.mapDtoToInvoice(dto)
          );
          this.invoices.set(invoices);
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

  downloadInvoice(invoice: EmployeeInvoice): void {
    if (!invoice.pdfBlobName) {
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
