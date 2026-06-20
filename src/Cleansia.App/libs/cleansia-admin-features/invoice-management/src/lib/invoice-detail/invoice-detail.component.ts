import { CommonModule } from '@angular/common';
import { ChangeDetectionStrategy, Component, inject, OnDestroy, OnInit } from '@angular/core';
import { ActivatedRoute, Router } from '@angular/router';
import {
  EmployeeInvoiceDetailDto,
  EmployeeInvoiceStatus,
  OrderEmployeePayDto,
} from '@cleansia/admin-services';
import {
  CleansiaButtonComponent,
  CleansiaLoaderComponent,
  CleansiaSectionComponent,
  CleansiaTableComponent,
  CleansiaTitleComponent,
  TableColumn,
  TableAction,
} from '@cleansia/components';
import { CleansiaAdminRoute, Policy } from '@cleansia/services';
import { CleansiaPermissionDirective } from '@cleansia/directives';
import { TranslatePipe, TranslateService } from '@ngx-translate/core';
import { DialogService } from 'primeng/dynamicdialog';
import { ToastModule } from 'primeng/toast';
import {
  getInvoicePdfState,
  getInvoicePdfStateClass,
  getInvoicePdfStateLabelKey,
  getInvoiceStatusClass,
  InvoicePdfState,
} from '../invoice-management/invoice-management.models';
import { AdminPayrollOpsComponent } from './components';
import { InvoiceDetailFacade } from './invoice-detail.facade';
import { getOrderPaysTableDefinition } from './invoice-detail.models';

@Component({
  selector: 'cleansia-admin-invoice-detail',
  standalone: true,
  imports: [
    CommonModule,
    CleansiaButtonComponent,
    TranslatePipe,
    CleansiaTitleComponent,
    CleansiaLoaderComponent,
    CleansiaSectionComponent,
    CleansiaTableComponent,
    ToastModule,
    CleansiaPermissionDirective,
    AdminPayrollOpsComponent,
  ],
  templateUrl: './invoice-detail.component.html',
  providers: [InvoiceDetailFacade, DialogService],
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class InvoiceDetailComponent implements OnInit, OnDestroy {
  protected readonly facade = inject(InvoiceDetailFacade);
  private readonly route = inject(ActivatedRoute);
  private readonly router = inject(Router);
  private readonly translate = inject(TranslateService);

  readonly EmployeeInvoiceStatus = EmployeeInvoiceStatus;
  protected readonly Policy = Policy;

  columns!: TableColumn<OrderEmployeePayDto>[];
  actions!: TableAction<OrderEmployeePayDto>[];

  ngOnInit(): void {
    const invoiceId = this.route.snapshot.paramMap.get('invoiceId');
    if (invoiceId) {
      this.facade.loadInvoiceDetail(invoiceId);
    } else {
      this.router.navigate([CleansiaAdminRoute.INVOICE_MANAGEMENT]);
    }

    const tableDef = getOrderPaysTableDefinition(this.translate);
    this.columns = tableDef.columns;
    this.actions = tableDef.actions;
  }

  ngOnDestroy(): void {
    this.facade.ngOnDestroy();
  }

  goBack(): void {
    this.router.navigate([CleansiaAdminRoute.INVOICE_MANAGEMENT]);
  }

  getInvoiceStatusClass(status: EmployeeInvoiceStatus | undefined): string {
    return getInvoiceStatusClass(status);
  }

  getPdfState(invoice: EmployeeInvoiceDetailDto): InvoicePdfState {
    return getInvoicePdfState(invoice);
  }

  getPdfStateClass(invoice: EmployeeInvoiceDetailDto): string {
    return getInvoicePdfStateClass(getInvoicePdfState(invoice));
  }

  getPdfStateLabelKey(invoice: EmployeeInvoiceDetailDto): string {
    return getInvoicePdfStateLabelKey(getInvoicePdfState(invoice));
  }

  onApprove(): void {
    this.facade.approveInvoice();
  }

  onMarkPaid(): void {
    this.facade.markAsPaid();
  }

  onCancel(): void {
    this.facade.openCancelDialog();
  }

  onDownload(): void {
    this.facade.downloadInvoice();
  }

  onRegeneratePdf(): void {
    this.facade.regeneratePdf();
  }

  onOpsChanged(): void {
    const invoiceId = this.facade.invoice()?.id;
    if (invoiceId) {
      this.facade.loadInvoiceDetail(invoiceId);
    }
  }
}
