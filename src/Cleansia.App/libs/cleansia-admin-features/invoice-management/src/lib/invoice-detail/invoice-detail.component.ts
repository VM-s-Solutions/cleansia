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
  CleansiaStatusBadgeComponent,
  CleansiaTableComponent,
  CleansiaTitleComponent,
  TableColumn,
  TableAction,
} from '@cleansia/components';
import { CleansiaAdminRoute, Policy } from '@cleansia/services';
import { CleansiaPermissionDirective } from '@cleansia/directives';
import { TranslatePipe, TranslateService } from '@ngx-translate/core';
import { ConfirmationService } from 'primeng/api';
import { ConfirmDialogModule } from 'primeng/confirmdialog';
import { DialogService } from 'primeng/dynamicdialog';
import { ToastModule } from 'primeng/toast';
import {
  getInvoicePdfState,
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
    CleansiaStatusBadgeComponent,
    CleansiaTableComponent,
    ToastModule,
    ConfirmDialogModule,
    CleansiaPermissionDirective,
    AdminPayrollOpsComponent,
  ],
  templateUrl: './invoice-detail.component.html',
  providers: [InvoiceDetailFacade, DialogService, ConfirmationService],
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class InvoiceDetailComponent implements OnInit, OnDestroy {
  protected readonly facade = inject(InvoiceDetailFacade);
  private readonly route = inject(ActivatedRoute);
  private readonly router = inject(Router);
  private readonly translate = inject(TranslateService);
  private readonly confirmationService = inject(ConfirmationService);

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

  getPdfState(invoice: EmployeeInvoiceDetailDto): InvoicePdfState {
    return getInvoicePdfState(invoice);
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

  confirmAssignVariableSymbol(): void {
    this.confirmationService.confirm({
      header: this.translate.instant(
        'pages.invoice_detail.assign_variable_symbol_confirm.title'
      ),
      message: this.translate.instant(
        'pages.invoice_detail.assign_variable_symbol_confirm.message'
      ),
      icon: 'pi pi-exclamation-triangle',
      acceptLabel: this.translate.instant(
        'pages.invoice_detail.assign_variable_symbol_confirm.yes'
      ),
      rejectLabel: this.translate.instant('global.actions.cancel'),
      accept: () => this.facade.assignVariableSymbol(),
    });
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
