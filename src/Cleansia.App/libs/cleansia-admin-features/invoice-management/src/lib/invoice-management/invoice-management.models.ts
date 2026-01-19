import { TemplateRef } from '@angular/core';
import {
  EmployeeInvoiceDto,
  EmployeeInvoiceStatus,
} from '@cleansia/admin-services';
import { TableColumn, TableAction } from '@cleansia/components';
import { TranslateService } from '@ngx-translate/core';

export function getInvoiceTableColumns(
  translate: TranslateService,
  statusTemplate?: TemplateRef<EmployeeInvoiceDto>
): TableColumn<EmployeeInvoiceDto>[] {
  return [
    {
      id: 'invoiceNumber',
      field: 'invoiceNumber',
      header: 'pages.invoice_management.invoice_number',
      sortable: true,
      width: '12%',
    },
    {
      id: 'employeeName',
      field: 'employeeName',
      header: 'pages.invoice_management.employee_name',
      sortable: true,
      width: '15%',
    },
    {
      id: 'payPeriodLabel',
      field: 'payPeriodLabel',
      header: 'pages.invoice_management.pay_period',
      sortable: true,
      width: '12%',
    },
    {
      id: 'totalOrders',
      field: 'totalOrders',
      header: 'pages.invoice_management.total_orders',
      sortable: true,
      width: '8%',
    },
    {
      id: 'totalAmount',
      field: 'totalAmount',
      header: 'pages.invoice_management.total_amount',
      sortable: true,
      width: '12%',
      getValue: (row: EmployeeInvoiceDto) => {
        const currency = row.currencyCode || 'CZK';
        return `${row.totalAmount?.toFixed(2)} ${currency}`;
      },
    },
    {
      id: 'status',
      field: 'status',
      header: 'pages.invoice_management.status',
      sortable: true,
      width: '10%',
      customTemplate: statusTemplate,
    },
    {
      id: 'generatedAt',
      field: 'generatedAt',
      header: 'pages.invoice_management.generated_at',
      sortable: true,
      width: '10%',
      getValue: (row: EmployeeInvoiceDto) => {
        if (!row.generatedAt) return '';
        const date =
          row.generatedAt instanceof Date
            ? row.generatedAt
            : new Date(row.generatedAt);
        return date.toLocaleDateString('cs-CZ');
      },
    },
  ];
}

export function getInvoiceTableActions(
  defs: {
    onViewDetails: (row: EmployeeInvoiceDto) => void;
    onDownload: (row: EmployeeInvoiceDto) => void;
  },
  translate: TranslateService
): TableAction<EmployeeInvoiceDto>[] {
  return [
    {
      icon: 'pi pi-eye',
      onClick: (row: EmployeeInvoiceDto) => defs.onViewDetails(row),
      color: 'info',
      tooltip: translate.instant('pages.invoice_management.view_details'),
    },
    {
      icon: 'pi pi-download',
      onClick: (row: EmployeeInvoiceDto) => defs.onDownload(row),
      color: 'primary',
      tooltip: translate.instant('pages.invoice_management.download'),
      visible: (row: EmployeeInvoiceDto) => !!row.pdfBlobName,
    },
  ];
}

export function getInvoiceStatusClass(
  status: EmployeeInvoiceStatus | undefined
): string {
  if (!status) return 'invoice-status-badge status-pending';
  switch (status) {
    case EmployeeInvoiceStatus.Pending:
      return 'invoice-status-badge status-pending';
    case EmployeeInvoiceStatus.Approved:
      return 'invoice-status-badge status-approved';
    case EmployeeInvoiceStatus.Paid:
      return 'invoice-status-badge status-paid';
    case EmployeeInvoiceStatus.Disputed:
      return 'invoice-status-badge status-disputed';
    case EmployeeInvoiceStatus.Rejected:
      return 'invoice-status-badge status-rejected';
    case EmployeeInvoiceStatus.Cancelled:
      return 'invoice-status-badge status-cancelled';
    default:
      return 'invoice-status-badge status-pending';
  }
}
