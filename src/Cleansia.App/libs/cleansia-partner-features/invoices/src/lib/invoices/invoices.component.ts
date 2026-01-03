import { CommonModule } from '@angular/common';
import {
  AfterViewInit,
  ChangeDetectorRef,
  Component,
  inject,
  TemplateRef,
  viewChild,
} from '@angular/core';
import { Router } from '@angular/router';
import {
  CleansiaLanguageSwitcherComponent,
  CleansiaLoaderComponent,
  CleansiaSectionComponent,
  CleansiaTableComponent,
  CleansiaTitleComponent,
  TableDefinition,
} from '@cleansia/components';
import { SortDefinition, SortDirection } from '@cleansia/partner-services';
import { TranslatePipe, TranslateService } from '@ngx-translate/core';
import { ButtonModule } from 'primeng/button';
import { TableModule } from 'primeng/table';
import { ToastModule } from 'primeng/toast';
import { EmployeeInvoice, InvoicesFacade } from './invoices.facade';
import { getInvoicesTableDefinition } from './invoices.models';

@Component({
  selector: 'cleansia-partner-invoices',
  standalone: true,
  imports: [
    TableModule,
    ToastModule,
    CommonModule,
    ButtonModule,
    TranslatePipe,
    CleansiaTableComponent,
    CleansiaTitleComponent,
    CleansiaLoaderComponent,
    CleansiaSectionComponent,
    CleansiaLanguageSwitcherComponent,
  ],
  templateUrl: './invoices.component.html',
  providers: [InvoicesFacade],
})
export class InvoicesComponent implements AfterViewInit {
  private readonly router = inject(Router);
  private readonly cd = inject(ChangeDetectorRef);
  protected readonly facade = inject(InvoicesFacade);
  private readonly translate = inject(TranslateService);

  statusTemplate = viewChild<TemplateRef<any>>('statusTemplate');

  invoicesTableDefinition!: TableDefinition<EmployeeInvoice>;

  private lastSortField: string | null = null;
  private lastSortOrder: number | null = null;

  ngAfterViewInit(): void {
    this.invoicesTableDefinition = getInvoicesTableDefinition(
      {
        onViewDetails: this.viewInvoiceDetails.bind(this),
        onDownload: this.downloadInvoice.bind(this),
      },
      this.translate,
      this.statusTemplate()
    );

    this.cd.detectChanges();
  }

  onSortChange(event: { field: string; order: number }): void {
    // Check if sort actually changed to prevent duplicate requests
    if (
      event.field === this.lastSortField &&
      event.order === this.lastSortOrder
    ) {
      return;
    }

    // Update last sort state
    this.lastSortField = event.field;
    this.lastSortOrder = event.order;

    const sortDef = [
      new SortDefinition({
        field: event.field,
        direction:
          event.order === 1
            ? SortDirection.Ascending
            : SortDirection.Descending,
      }),
    ];
    this.facade.updateSort(sortDef);
  }

  viewInvoiceDetails(invoice: EmployeeInvoice): void {
    this.router.navigate(['/invoices', invoice.id]);
  }

  downloadInvoice(invoice: EmployeeInvoice): void {
    this.facade.downloadInvoice(invoice);
  }

  getStatusClass(invoice: EmployeeInvoice): string {
    const statusName = invoice.status.toLowerCase();
    return `status-badge status-${statusName}`;
  }
}
