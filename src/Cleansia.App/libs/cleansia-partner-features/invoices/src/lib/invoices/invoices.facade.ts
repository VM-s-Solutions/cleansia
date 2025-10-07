import { Injectable, OnDestroy, inject, signal } from '@angular/core';
import {
  Client,
  EmployeeInvoiceDto,
  EmployeeInvoiceDtoPagedData,
  SnackbarService,
  SortDefinition,
} from '@cleansia/services';
import { Subject, catchError, of, takeUntil } from 'rxjs';

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
  status: 'Pending' | 'Approved' | 'Paid' | 'Disputed' | 'Rejected';
  pdfBlobUrl?: string;
  generatedAt: Date;
  approvedAt?: Date;
  approvedBy?: string;
  paidAt?: Date;
  adminNotes?: string;
  bankTransferNote?: string;
}

@Injectable()
export class InvoicesFacade implements OnDestroy {
  private readonly snackbarService = inject(SnackbarService);
  private readonly client = inject(Client);
  private readonly destroy$ = new Subject<void>();

  // Signals for reactive data
  invoices = signal<EmployeeInvoice[]>([]);
  loading = signal<boolean>(false);
  
  private currentEmployeeId = signal<string | null>(null);
  private currentSort = signal<SortDefinition[]>([]);

  constructor() {
    // Get current employee ID
    this.loadCurrentEmployee();
  }

  ngOnDestroy(): void {
    this.destroy$.next();
    this.destroy$.complete();
  }

  private loadCurrentEmployee(): void {
    this.client.employeeClient
      .getCurrentEmployee()
      .pipe(
        takeUntil(this.destroy$),
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
      status: dto.status! as 'Pending' | 'Approved' | 'Paid' | 'Disputed' | 'Rejected',
      pdfBlobUrl: dto.pdfBlobUrl ?? undefined,
      generatedAt: new Date(dto.generatedAt!),
      approvedAt: dto.approvedAt ? new Date(dto.approvedAt) : undefined,
      approvedBy: dto.approvedBy ?? undefined,
      paidAt: dto.paidAt ? new Date(dto.paidAt) : undefined,
      adminNotes: dto.adminNotes ?? undefined,
      bankTransferNote: dto.bankTransferNote ?? undefined,
    };
  }

  loadInvoices(offset = 0, limit = 20): void {
    const employeeId = this.currentEmployeeId();
    
    if (!employeeId) {
      return;
    }

    this.loading.set(true);

    this.client.employeePayrollClient
      .getPagedInvoices(employeeId, undefined, undefined, this.currentSort(), offset, limit)
      .pipe(
        takeUntil(this.destroy$),
        catchError((error) => {
          console.error('Failed to load invoices:', error);
          this.snackbarService.showError('Failed to load invoices. Please try again.');
          this.loading.set(false);
          return of(null);
        })
      )
      .subscribe((pagedData: EmployeeInvoiceDtoPagedData | null) => {
        if (pagedData && pagedData.data) {
          const invoices = pagedData.data.map((dto: EmployeeInvoiceDto) => this.mapDtoToInvoice(dto));
          this.invoices.set(invoices);
        } else {
          this.invoices.set([]);
        }
        this.loading.set(false);
      });
  }

  updateSort(sort: SortDefinition[]): void {
    this.currentSort.set(sort);
    this.loadInvoices();
  }

  downloadInvoice(invoice: EmployeeInvoice): void {
    if (invoice.pdfBlobUrl) {
      // Download from blob URL
      window.open(invoice.pdfBlobUrl, '_blank');
      this.snackbarService.showSuccessTranslated('global.messages.invoices.invoice_downloaded');
    } else {
      this.snackbarService.showErrorTranslated('pages.invoices.pdf_not_available');
    }
  }

  viewInvoiceDetails(invoice: EmployeeInvoice): void {
    // TODO: Navigate to invoice details page
    console.log('View details for invoice:', invoice.id);
  }
}
