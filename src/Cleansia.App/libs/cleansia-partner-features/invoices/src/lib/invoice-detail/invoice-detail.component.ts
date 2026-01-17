import { CommonModule } from '@angular/common';
import { Component, computed, inject, OnInit } from '@angular/core';
import { ActivatedRoute, Router } from '@angular/router';
import {
  CleansiaButtonComponent,
  CleansiaLanguageSwitcherComponent,
  CleansiaLoaderComponent,
  CleansiaSectionComponent,
  CleansiaTableComponent,
  TableColumn,
} from '@cleansia/components';
import {
  EmployeeInvoiceStatus,
  OrderEmployeePayDto,
} from '@cleansia/partner-services';
import { CleansiaPartnerRoute } from '@cleansia/services';
import { TranslatePipe, TranslateService } from '@ngx-translate/core';
import { InvoiceDetailFacade } from './invoice-detail.facade';

@Component({
  selector: 'cleansia-partner-invoice-detail',
  standalone: true,
  imports: [
    CommonModule,
    TranslatePipe,
    CleansiaButtonComponent,
    CleansiaLoaderComponent,
    CleansiaSectionComponent,
    CleansiaTableComponent,
    CleansiaLanguageSwitcherComponent,
  ],
  templateUrl: './invoice-detail.component.html',
  providers: [InvoiceDetailFacade],
})
export class InvoiceDetailComponent implements OnInit {
  protected readonly facade = inject(InvoiceDetailFacade);
  private readonly route = inject(ActivatedRoute);
  private readonly router = inject(Router);
  private readonly translate = inject(TranslateService);

  protected readonly invoiceDetail = this.facade.invoiceDetail;
  protected readonly loading = this.facade.loading;
  protected readonly error = this.facade.error;

  protected readonly orderPaysColumns = computed(() =>
    this.getOrderPaysColumns()
  );

  ngOnInit(): void {
    const invoiceId = this.route.snapshot.paramMap.get('id');
    if (invoiceId) {
      this.facade.loadInvoiceDetail(invoiceId);
    } else {
      this.facade.error.set('No invoice ID provided');
    }
  }

  navigateToInvoices(): void {
    this.router.navigate([CleansiaPartnerRoute.INVOICES]);
  }

  retryLoadInvoice(): void {
    const invoiceId = this.route.snapshot.paramMap.get('id');
    if (invoiceId) {
      this.facade.loadInvoiceDetail(invoiceId);
    }
  }

  downloadPdf(): void {
    this.facade.downloadPdf();
  }

  printInvoice(): void {
    this.facade.printInvoice();
  }

  getStatusClass(status: EmployeeInvoiceStatus): string {
    const statusString = this.getStatusString(status);
    return `status-badge status-${statusString}`;
  }

  getStatusString(status: EmployeeInvoiceStatus): string {
    switch (status) {
      case EmployeeInvoiceStatus.Pending:
        return 'pending';
      case EmployeeInvoiceStatus.Approved:
        return 'approved';
      case EmployeeInvoiceStatus.Paid:
        return 'paid';
      case EmployeeInvoiceStatus.Disputed:
        return 'disputed';
      case EmployeeInvoiceStatus.Rejected:
        return 'rejected';
      case EmployeeInvoiceStatus.Cancelled:
        return 'cancelled';
      default:
        return 'pending';
    }
  }

  private getOrderPaysColumns(): TableColumn<OrderEmployeePayDto>[] {
    return [
      {
        id: 'orderNumber',
        field: 'orderNumber',
        header: this.translate.instant('pages.invoice_detail.order_number'),
        sortable: false,
      },
      {
        id: 'basePay',
        field: 'basePay',
        header: this.translate.instant('pages.invoice_detail.base_pay'),
        sortable: false,
        align: 'right',
        getValue: (pay?: OrderEmployeePayDto) =>
          pay
            ? `${pay.basePay?.toFixed(2)} ${this.invoiceDetail()?.currencyCode}`
            : '',
      },
      {
        id: 'extrasPay',
        field: 'extrasPay',
        header: this.translate.instant('pages.invoice_detail.extras_pay'),
        sortable: false,
        align: 'right',
        getValue: (pay?: OrderEmployeePayDto) =>
          pay
            ? `${pay.extrasPay?.toFixed(2)} ${this.invoiceDetail()?.currencyCode}`
            : '',
      },
      {
        id: 'expensesPay',
        field: 'expensesPay',
        header: this.translate.instant('pages.invoice_detail.expenses_pay'),
        sortable: false,
        align: 'right',
        getValue: (pay?: OrderEmployeePayDto) =>
          pay
            ? `${pay.expensesPay?.toFixed(2)} ${this.invoiceDetail()?.currencyCode}`
            : '',
      },
      {
        id: 'bonusPay',
        field: 'bonusPay',
        header: this.translate.instant('pages.invoice_detail.bonus_pay'),
        sortable: false,
        align: 'right',
        getValue: (pay?: OrderEmployeePayDto) =>
          pay
            ? `${pay.bonusPay?.toFixed(2)} ${this.invoiceDetail()?.currencyCode}`
            : '',
      },
      {
        id: 'deductionPay',
        field: 'deductionPay',
        header: this.translate.instant('pages.invoice_detail.deduction_pay'),
        sortable: false,
        align: 'right',
        getValue: (pay?: OrderEmployeePayDto) =>
          pay
            ? `${pay.deductionPay?.toFixed(2)} ${this.invoiceDetail()?.currencyCode}`
            : '',
      },
      {
        id: 'totalPay',
        field: 'totalPay',
        header: this.translate.instant('pages.invoice_detail.total_pay'),
        sortable: false,
        align: 'right',
        getValue: (pay?: OrderEmployeePayDto) =>
          pay
            ? `${pay.totalPay?.toFixed(2)} ${this.invoiceDetail()?.currencyCode}`
            : '',
      },
    ];
  }
}
