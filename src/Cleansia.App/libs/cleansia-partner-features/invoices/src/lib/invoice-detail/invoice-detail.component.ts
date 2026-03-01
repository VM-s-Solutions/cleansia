import { CommonModule } from '@angular/common';
import { Component, computed, inject, OnInit } from '@angular/core';
import { ActivatedRoute, Router } from '@angular/router';
import {
  CleansiaButtonComponent,
  CleansiaDetailSkeletonComponent,
  CleansiaSectionComponent,
  CleansiaTableComponent,
} from '@cleansia/components';
import { EmployeeInvoiceStatus } from '@cleansia/partner-services';
import { CleansiaPartnerRoute } from '@cleansia/services';
import { TranslatePipe } from '@ngx-translate/core';
import { InvoiceDetailFacade } from './invoice-detail.facade';
import { getOrderPaysTableDefinition } from './invoice-detail.models';

@Component({
  selector: 'cleansia-partner-invoice-detail',
  standalone: true,
  imports: [
    CommonModule,
    TranslatePipe,
    CleansiaButtonComponent,
    CleansiaDetailSkeletonComponent,
    CleansiaSectionComponent,
    CleansiaTableComponent,
  ],
  templateUrl: './invoice-detail.component.html',
  providers: [InvoiceDetailFacade],
})
export class InvoiceDetailComponent implements OnInit {
  protected readonly facade = inject(InvoiceDetailFacade);
  private readonly route = inject(ActivatedRoute);
  private readonly router = inject(Router);

  protected readonly invoiceDetail = this.facade.invoiceDetail;
  protected readonly loading = this.facade.loading;
  protected readonly error = this.facade.error;

  protected readonly orderPaysColumns = computed(() => {
    const currencyCode = this.invoiceDetail()?.currencyCode || 'EUR';
    return getOrderPaysTableDefinition(currencyCode).columns;
  });

  ngOnInit(): void {
    const invoiceId = this.route.snapshot.paramMap.get('invoiceId');
    if (invoiceId) {
      this.facade.loadInvoiceDetail(invoiceId);
    } else {
      this.navigateToInvoices();
    }
  }

  navigateToInvoices(): void {
    this.router.navigate([CleansiaPartnerRoute.INVOICES]);
  }

  retryLoadInvoice(): void {
    const invoiceId = this.route.snapshot.paramMap.get('invoiceId');
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

}
