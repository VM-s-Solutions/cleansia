import { CommonModule } from '@angular/common';
import { Component, computed, inject, OnInit } from '@angular/core';
import { ActivatedRoute, Router } from '@angular/router';
import {
  CleansiaButtonComponent,
  CleansiaLanguageSwitcherComponent,
  CleansiaLoaderComponent,
  CleansiaSectionComponent,
  CleansiaTableComponent,
  TableDefinition,
} from '@cleansia/components';
import { OrderEmployeePayDto } from '@cleansia/services';
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

  protected readonly orderPaysTableDefinition = computed(() =>
    this.getOrderPaysTableDefinition()
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
    this.router.navigate(['/invoices']);
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

  getStatusClass(status: string): string {
    const statusLower = status.toLowerCase();
    return `status-badge status-${statusLower}`;
  }

  private getOrderPaysTableDefinition(): TableDefinition<OrderEmployeePayDto> {
    return {
      columns: [
        {
          id: 'orderNumber',
          headerName: this.translate.instant(
            'pages.invoice_detail.order_number'
          ),
          value: 'orderNumber',
          sortable: false,
          columnClass: 'font-semibold',
        },
        {
          id: 'basePay',
          headerName: this.translate.instant('pages.invoice_detail.base_pay'),
          value: (pay?: OrderEmployeePayDto) =>
            pay
              ? `${pay.basePay?.toFixed(2)} ${
                  this.invoiceDetail()?.currencyCode
                }`
              : '',
          sortable: false,
          columnClass: 'text-right',
        },
        {
          id: 'extrasPay',
          headerName: this.translate.instant('pages.invoice_detail.extras_pay'),
          value: (pay?: OrderEmployeePayDto) =>
            pay
              ? `${pay.extrasPay?.toFixed(2)} ${
                  this.invoiceDetail()?.currencyCode
                }`
              : '',
          sortable: false,
          columnClass: 'text-right',
        },
        {
          id: 'expensesPay',
          headerName: this.translate.instant(
            'pages.invoice_detail.expenses_pay'
          ),
          value: (pay?: OrderEmployeePayDto) =>
            pay
              ? `${pay.expensesPay?.toFixed(2)} ${
                  this.invoiceDetail()?.currencyCode
                }`
              : '',
          sortable: false,
          columnClass: 'text-right',
        },
        {
          id: 'bonusPay',
          headerName: this.translate.instant('pages.invoice_detail.bonus_pay'),
          value: (pay?: OrderEmployeePayDto) =>
            pay
              ? `${pay.bonusPay?.toFixed(2)} ${
                  this.invoiceDetail()?.currencyCode
                }`
              : '',
          sortable: false,
          columnClass: 'text-right',
        },
        {
          id: 'deductionPay',
          headerName: this.translate.instant(
            'pages.invoice_detail.deduction_pay'
          ),
          value: (pay?: OrderEmployeePayDto) =>
            pay
              ? `${pay.deductionPay?.toFixed(2)} ${
                  this.invoiceDetail()?.currencyCode
                }`
              : '',
          sortable: false,
          columnClass: 'text-right text-red-600',
        },
        {
          id: 'totalPay',
          headerName: this.translate.instant('pages.invoice_detail.total_pay'),
          value: (pay?: OrderEmployeePayDto) =>
            pay
              ? `${pay.totalPay?.toFixed(2)} ${
                  this.invoiceDetail()?.currencyCode
                }`
              : '',
          sortable: false,
          columnClass: 'text-right font-bold',
        },
      ],
    };
  }
}
