import { TemplateRef } from '@angular/core';
import {
  EmployeeInvoiceDto,
  EmployeeInvoiceStatus,
} from '@cleansia/admin-services';
import { TableDefinition } from '@cleansia/components';
import { TranslateService } from '@ngx-translate/core';

export function getInvoiceTableDefinition(
  defs: {
    onViewDetails: (row: EmployeeInvoiceDto) => void;
    onDownload: (row: EmployeeInvoiceDto) => void;
  },
  translate: TranslateService,
  statusTemplate?: TemplateRef<EmployeeInvoiceDto>
): TableDefinition<EmployeeInvoiceDto> {
  return {
    columns: [
      {
        id: 'invoiceNumber',
        headerName: translate.instant(
          'pages.invoice_management.invoice_number'
        ),
        value: 'invoiceNumber',
        sortable: true,
        sortField: 'invoiceNumber',
        columnClass: 'width-12',
      },
      {
        id: 'employeeName',
        headerName: translate.instant('pages.invoice_management.employee_name'),
        value: 'employeeName',
        sortable: true,
        sortField: 'employeeName',
        columnClass: 'width-15',
      },
      {
        id: 'payPeriodLabel',
        headerName: translate.instant('pages.invoice_management.pay_period'),
        value: 'payPeriodLabel',
        sortable: true,
        sortField: 'payPeriodLabel',
        columnClass: 'width-12',
      },
      {
        id: 'totalOrders',
        headerName: translate.instant('pages.invoice_management.total_orders'),
        value: 'totalOrders',
        sortable: true,
        sortField: 'totalOrders',
        columnClass: 'width-8',
      },
      {
        id: 'totalAmount',
        headerName: translate.instant('pages.invoice_management.total_amount'),
        value: (row?: EmployeeInvoiceDto) => {
          if (!row) return '';
          const currency = row.currencyCode || 'CZK';
          return `${row.totalAmount?.toFixed(2)} ${currency}`;
        },
        sortable: true,
        sortField: 'totalAmount',
        columnClass: 'width-12',
      },
      {
        id: 'status',
        headerName: translate.instant('pages.invoice_management.status'),
        template: statusTemplate,
        sortable: true,
        sortField: 'status',
        columnClass: 'width-10',
      },
      {
        id: 'generatedAt',
        headerName: translate.instant('pages.invoice_management.generated_at'),
        value: (row?: EmployeeInvoiceDto) => {
          if (!row?.generatedAt) return '';
          const date =
            row.generatedAt instanceof Date
              ? row.generatedAt
              : new Date(row.generatedAt);
          return date.toLocaleDateString('cs-CZ');
        },
        sortable: true,
        sortField: 'generatedAt',
        columnClass: 'width-10',
      },
      {
        id: 'actions',
        headerName: translate.instant('pages.invoice_management.actions'),
        columnActions: [
          {
            icon: 'pi pi-eye',
            onClick: (row: EmployeeInvoiceDto) => defs.onViewDetails(row),
            buttonPalette: 'p-button-info p-button-sm',
            tooltip: {
              title: translate.instant('pages.invoice_management.view_details'),
              position: 'above',
            },
          },
          {
            icon: 'pi pi-download',
            onClick: (row: EmployeeInvoiceDto) => defs.onDownload(row),
            buttonPalette: 'p-button-secondary p-button-sm',
            tooltip: {
              title: translate.instant('pages.invoice_management.download'),
              position: 'above',
            },
            visible: (row: EmployeeInvoiceDto) => !!row.pdfBlobName,
          },
        ],
        columnClass: 'width-10',
      },
    ],
  };
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
