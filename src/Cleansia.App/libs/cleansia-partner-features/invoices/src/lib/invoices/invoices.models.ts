import { TemplateRef } from '@angular/core';
import { TranslateService } from '@ngx-translate/core';
import { TableDefinition } from '@cleansia/components';
import { EmployeeInvoice } from './invoices.facade';

export interface InvoicesActions {
  onViewDetails: (invoice: EmployeeInvoice) => void;
  onDownload: (invoice: EmployeeInvoice) => void;
}

export function getInvoicesTableDefinition(
  actions: InvoicesActions,
  translate: TranslateService,
  statusTemplate?: TemplateRef<any>
): TableDefinition<EmployeeInvoice> {
  return {
    columns: [
      {
        id: 'invoiceNumber',
        headerName: translate.instant('pages.invoices.invoice_number'),
        value: 'invoiceNumber',
        sortable: true,
        columnClass: 'font-semibold',
      },
      {
        id: 'payPeriodLabel',
        headerName: translate.instant('pages.invoices.pay_period'),
        value: 'payPeriodLabel',
        sortable: true,
      },
      {
        id: 'generatedAt',
        headerName: translate.instant('pages.invoices.generated_date'),
        value: (invoice?: EmployeeInvoice) =>
          invoice ? new Date(invoice.generatedAt).toLocaleDateString('cs-CZ') : '',
        sortable: true,
      },
      {
        id: 'totalOrders',
        headerName: translate.instant('pages.invoices.total_orders'),
        value: 'totalOrders',
        sortable: true,
      },
      {
        id: 'totalAmount',
        headerName: translate.instant('pages.invoices.total_amount'),
        value: (invoice?: EmployeeInvoice) =>
          invoice ? new Intl.NumberFormat('cs-CZ', {
            style: 'currency',
            currency: invoice.currencyCode || 'CZK',
          }).format(invoice.totalAmount) : '',
        sortable: true,
      },
      {
        id: 'status',
        headerName: translate.instant('pages.invoices.status'),
        template: statusTemplate,
        sortable: true,
      },
      {
        id: 'actions',
        headerName: translate.instant('global.actions.actions'),
        columnClass: 'text-right',
        columnActions: [
          {
            icon: 'pi pi-eye',
            tooltip: { title: translate.instant('pages.invoices.view_details'), position: 'left' },
            buttonPalette: 'p-button-text p-button-sm',
            onClick: actions.onViewDetails,
          },
          {
            icon: 'pi pi-download',
            tooltip: { title: translate.instant('pages.invoices.download_pdf'), position: 'left' },
            buttonPalette: 'p-button-text p-button-sm',
            onClick: actions.onDownload,
            disabled: (invoice: EmployeeInvoice) => !invoice.pdfBlobUrl,
          },
        ],
      },
    ],
  };
}
