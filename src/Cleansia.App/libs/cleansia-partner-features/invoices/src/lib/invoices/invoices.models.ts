import { TemplateRef } from '@angular/core';
import { TableColumn, TableAction } from '@cleansia/components';
import { EmployeeInvoiceDto } from '@cleansia/partner-services';
import { formatMoney } from '@cleansia/utils';

export interface InvoicesActions {
  onDownload: (invoice: EmployeeInvoiceDto) => void;
}

export function getInvoicesTableDefinition(
  actions: InvoicesActions,
  statusTemplate?: TemplateRef<EmployeeInvoiceDto>
): { columns: TableColumn<EmployeeInvoiceDto>[]; actions: TableAction<EmployeeInvoiceDto>[] } {
  return {
    columns: [
      {
        id: 'invoiceNumber',
        field: 'invoiceNumber',
        header: 'pages.invoices.invoice_number',
        sortable: true,
      },
      {
        id: 'payPeriodLabel',
        field: 'payPeriodLabel',
        header: 'pages.invoices.pay_period',
        sortable: true,
      },
      {
        id: 'generatedAt',
        field: 'generatedAt',
        header: 'pages.invoices.generated_date',
        getValue: (invoice?: EmployeeInvoiceDto) =>
          invoice
            ? new Date(invoice.generatedAt).toLocaleDateString('en-GB')
            : '',
        sortable: true,
      },
      {
        id: 'totalOrders',
        field: 'totalOrders',
        header: 'pages.invoices.total_orders',
        sortable: true,
      },
      {
        id: 'totalAmount',
        field: 'totalAmount',
        header: 'pages.invoices.total_amount',
        getValue: (invoice?: EmployeeInvoiceDto) =>
          invoice ? formatMoney(invoice.totalAmount, invoice.currencyCode ?? '', 'en-GB', { fractionDigits: 2 }) : '',
        sortable: true,
        align: 'right',
      },
      {
        id: 'status',
        field: 'status',
        header: 'pages.invoices.status',
        customTemplate: statusTemplate,
        sortable: true,
      },
    ],
    actions: [
      {
        icon: 'pi pi-download',
        tooltip: 'pages.invoices.download_pdf',
        onClick: actions.onDownload,
        disabled: (invoice: EmployeeInvoiceDto) => !invoice.pdfBlobName,
      },
    ],
  };
}
