import { TemplateRef } from '@angular/core';
import { TableColumn, TableAction } from '@cleansia/components';
import { EmployeeInvoiceDto } from '@cleansia/partner-services';
import { formatDate, formatMoney, formatPayPeriodRange, localeFor } from '@cleansia/utils';

export interface InvoicesActions {
  onDownload: (invoice: EmployeeInvoiceDto) => void;
}

export function getInvoicesTableDefinition(
  actions: InvoicesActions,
  lang: string | undefined,
  statusTemplate?: TemplateRef<EmployeeInvoiceDto>
): { columns: TableColumn<EmployeeInvoiceDto>[]; actions: TableAction<EmployeeInvoiceDto>[] } {
  const money = (invoice: EmployeeInvoiceDto, amount: number): string =>
    formatMoney(amount, invoice.currencyCode ?? '', localeFor(lang), { fractionDigits: 2 });
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
        getValue: (invoice: EmployeeInvoiceDto) =>
          formatPayPeriodRange(invoice.payPeriodStartDate, invoice.payPeriodEndDate, lang),
      },
      {
        id: 'generatedAt',
        field: 'generatedAt',
        header: 'pages.invoices.generated_date',
        getValue: (invoice?: EmployeeInvoiceDto) => formatDate(invoice?.generatedAt, lang),
        sortable: true,
        numeric: true,
      },
      {
        id: 'totalOrders',
        field: 'totalOrders',
        header: 'pages.invoices.total_orders',
        numeric: true,
      },
      {
        id: 'totalAmount',
        field: 'totalAmount',
        header: 'pages.invoices.total_amount',
        getValue: (invoice: EmployeeInvoiceDto) => money(invoice, invoice.totalAmount),
        sortable: true,
        numeric: true,
      },
      {
        id: 'cashSetOffAmount',
        field: 'cashSetOffAmount',
        header: 'pages.invoices.cash_set_off',
        getValue: (invoice: EmployeeInvoiceDto) => money(invoice, invoice.cashSetOffAmount),
        numeric: true,
      },
      {
        id: 'transferAmount',
        field: 'transferAmount',
        header: 'pages.invoices.transfer_amount',
        getValue: (invoice: EmployeeInvoiceDto) => money(invoice, invoice.transferAmount),
        numeric: true,
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
