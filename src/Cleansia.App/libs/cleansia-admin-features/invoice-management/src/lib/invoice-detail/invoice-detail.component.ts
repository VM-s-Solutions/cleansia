import { CommonModule } from '@angular/common';
import { Component, inject, OnDestroy, OnInit } from '@angular/core';
import { ActivatedRoute, Router } from '@angular/router';
import {
  EmployeeInvoiceStatus,
  OrderEmployeePayDto,
} from '@cleansia/admin-services';
import {
  CleansiaButtonComponent,
  CleansiaLanguageSwitcherComponent,
  CleansiaLoaderComponent,
  CleansiaSectionComponent,
  CleansiaTableComponent,
  CleansiaTitleComponent,
  TableColumn,
  TableAction,
} from '@cleansia/components';
import { CleansiaAdminRoute } from '@cleansia/services';
import { TranslatePipe, TranslateService } from '@ngx-translate/core';
import { DialogService } from 'primeng/dynamicdialog';
import { ToastModule } from 'primeng/toast';
import { getInvoiceStatusClass } from '../invoice-management/invoice-management.models';
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
    CleansiaLanguageSwitcherComponent,
    CleansiaTableComponent,
    ToastModule,
  ],
  templateUrl: './invoice-detail.component.html',
  providers: [InvoiceDetailFacade, DialogService],
})
export class InvoiceDetailComponent implements OnInit, OnDestroy {
  protected readonly facade = inject(InvoiceDetailFacade);
  private readonly route = inject(ActivatedRoute);
  private readonly router = inject(Router);
  private readonly translate = inject(TranslateService);

  readonly EmployeeInvoiceStatus = EmployeeInvoiceStatus;

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
}
