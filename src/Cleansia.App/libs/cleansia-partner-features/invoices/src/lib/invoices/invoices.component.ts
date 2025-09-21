import { CommonModule } from '@angular/common';
import { Component, inject } from '@angular/core';
import { ReactiveFormsModule } from '@angular/forms';
import {
  CleansiaButtonComponent,
  CleansiaLanguageSwitcherComponent,
  CleansiaSectionComponent,
  CleansiaTableComponent,
  CleansiaTableColumn,
  CleansiaTableAction,
  CleansiaTitleComponent,
} from '@cleansia/components';
import { TranslatePipe } from '@ngx-translate/core';
import { MessageService } from 'primeng/api';
import { DialogModule } from 'primeng/dialog';
import { ToastModule } from 'primeng/toast';
import { InvoicesFacade, Invoice } from './invoices.facade';

@Component({
  selector: 'cleansia-partner-invoices',
  standalone: true,
  imports: [
    CommonModule,
    ToastModule,
    TranslatePipe,
    DialogModule,
    ReactiveFormsModule,
    CleansiaTitleComponent,
    CleansiaButtonComponent,
    CleansiaSectionComponent,
    CleansiaTableComponent,
    CleansiaLanguageSwitcherComponent,
  ],
  templateUrl: './invoices.component.html',
  providers: [InvoicesFacade, MessageService],
})
export class InvoicesComponent {
  protected readonly facade: InvoicesFacade = inject(InvoicesFacade);

  // Pending invoices table configuration
  pendingInvoicesColumns: CleansiaTableColumn[] = [
    { field: 'number', header: 'pages.invoices.invoice_number', sortable: true },
    { field: 'date', header: 'pages.invoices.date', pipe: 'date', sortable: true },
    { field: 'amount', header: 'pages.invoices.amount', pipe: 'currency', sortable: true },
    { field: 'dueDate', header: 'pages.invoices.due_date', pipe: 'date', sortable: true },
    { field: 'status', header: 'pages.invoices.status' },
  ];

  pendingInvoicesActions: CleansiaTableAction[] = [
    {
      label: 'pages.invoices.download',
      icon: 'pi pi-download',
      class: 'p-button-text p-button-sm',
      action: (invoice: Invoice) => this.facade.downloadInvoice(invoice),
    },
  ];

  // All invoices table configuration
  allInvoicesColumns: CleansiaTableColumn[] = [
    { field: 'number', header: 'pages.invoices.invoice_number', sortable: true },
    { field: 'date', header: 'pages.invoices.date', pipe: 'date', sortable: true },
    { field: 'amount', header: 'pages.invoices.amount', pipe: 'currency', sortable: true },
    { field: 'dueDate', header: 'pages.invoices.due_date', pipe: 'date', sortable: true },
    { field: 'status', header: 'pages.invoices.status' },
  ];

  allInvoicesActions: CleansiaTableAction[] = [
    {
      label: 'pages.invoices.download',
      icon: 'pi pi-download',
      class: 'p-button-outlined p-button-sm',
      action: (invoice: Invoice) => this.facade.downloadInvoice(invoice),
    },
    {
      label: 'pages.invoices.view_details',
      icon: 'pi pi-eye',
      class: 'p-button-outlined p-button-sm',
      action: (invoice: Invoice) => this.facade.viewInvoiceDetails(invoice),
    },
  ];

  getFieldValue(item: Record<string, unknown>, column: CleansiaTableColumn): unknown {
    const value = this.getNestedValue(item, column.field);
    return this.applyPipe(value, column.pipe);
  }

  private getNestedValue(obj: Record<string, unknown>, path: string): unknown {
    return path.split('.').reduce((current: unknown, prop: string) => {
      return (current as Record<string, unknown>)?.[prop];
    }, obj as unknown);
  }

  private applyPipe(value: unknown, pipe?: string): unknown {
    if (!pipe || value == null) return value;

    switch (pipe) {
      case 'date':
        return new Date(value as string | number | Date).toLocaleDateString('cs-CZ');
      case 'currency':
        return new Intl.NumberFormat('cs-CZ', {
          style: 'currency',
          currency: 'CZK'
        }).format(value as number);
      default:
        return value;
    }
  }
}
