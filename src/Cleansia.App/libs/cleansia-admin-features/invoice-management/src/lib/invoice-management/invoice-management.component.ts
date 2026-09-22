import {
  ChangeDetectionStrategy,
  Component,
  computed,
  inject,
  OnInit,
  TemplateRef,
  viewChild,
} from '@angular/core';
import { FormsModule, ReactiveFormsModule } from '@angular/forms';
import { Router } from '@angular/router';
import { EmployeeInvoiceDto, EmployeeInvoiceStatus } from '@cleansia/admin-services';
import {
  CleansiaCheckboxComponent,
  CleansiaFilterChipsComponent,
  CleansiaFilterDrawerComponent,
  CleansiaLoaderComponent,
  CleansiaSectionComponent,
  CleansiaSelectComponent,
  CleansiaStatusBadgeComponent,
  CleansiaTableComponent,
  CleansiaTitleComponent,
  ICleansiaSelectOption,
} from '@cleansia/components';
import { CleansiaAdminRoute } from '@cleansia/services';
import { TranslatePipe, TranslateService } from '@ngx-translate/core';
import { ToastModule } from 'primeng/toast';
import { TooltipModule } from 'primeng/tooltip';
import { InvoiceManagementFacade } from './invoice-management.facade';
import {
  getInvoicePdfState,
  getInvoiceTableActions,
  getInvoiceTableColumns,
  InvoicePdfState,
} from './invoice-management.models';

@Component({
  selector: 'cleansia-admin-invoice-management',
  standalone: true,
  imports: [
    CleansiaCheckboxComponent,
    CleansiaSelectComponent,
    TranslatePipe,
    CleansiaStatusBadgeComponent,
    CleansiaTableComponent,
    CleansiaTitleComponent,
    CleansiaLoaderComponent,
    CleansiaSectionComponent,
    CleansiaFilterDrawerComponent,
    CleansiaFilterChipsComponent,
    FormsModule,
    ReactiveFormsModule,
    ToastModule,
    TooltipModule,
  ],
  templateUrl: './invoice-management.component.html',
  providers: [InvoiceManagementFacade],
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class InvoiceManagementComponent implements OnInit {
  private readonly router = inject(Router);
  protected readonly facade = inject(InvoiceManagementFacade);
  private readonly translate = inject(TranslateService);

  private readonly statusTemplate = viewChild<TemplateRef<EmployeeInvoiceDto>>('statusTemplate');
  private readonly pdfStatusTemplate = viewChild<TemplateRef<EmployeeInvoiceDto>>('pdfStatusTemplate');

  readonly EmployeeInvoiceStatus = EmployeeInvoiceStatus;

  protected readonly table = computed(() => {
    this.facade.lang();
    return {
      columns: getInvoiceTableColumns(this.translate, this.statusTemplate(), this.pdfStatusTemplate()),
      actions: getInvoiceTableActions(
        {
          onViewDetails: (row) => this.viewInvoiceDetails(row),
          onDownload: (row) => this.facade.downloadInvoice(row),
          onRetryPdf: (row) => this.facade.retryPdf(row),
        },
        this.translate
      ),
    };
  });

  readonly currencyOptions = computed<ICleansiaSelectOption[]>(() =>
    this.facade.currencies().map((c) => ({ label: c.code, value: c.id }))
  );

  ngOnInit(): void {
    this.facade.loadCurrencies();
    this.facade.loadInvoices();
  }

  viewInvoiceDetails(invoice: EmployeeInvoiceDto): void {
    this.router.navigate([CleansiaAdminRoute.INVOICE_MANAGEMENT, invoice.id]);
  }

  getPdfState(invoice: EmployeeInvoiceDto): InvoicePdfState {
    return getInvoicePdfState(invoice);
  }

  toggleStatus(status: EmployeeInvoiceStatus): void {
    this.facade.setStatus(status, !this.facade.isStatusChecked(status));
  }
}
