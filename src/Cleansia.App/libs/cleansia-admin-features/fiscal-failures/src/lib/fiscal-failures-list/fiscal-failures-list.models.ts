import { TemplateRef } from '@angular/core';
import { FiscalFailureDto } from '@cleansia/admin-services';
import { TableColumn, TableAction } from '@cleansia/components';
import { TranslateService } from '@ngx-translate/core';

export function getFiscalFailureTableColumns(
  translate: TranslateService,
  errorKindTemplate?: TemplateRef<FiscalFailureDto>
): TableColumn<FiscalFailureDto>[] {
  return [
    {
      id: 'receiptNumber',
      field: 'receiptNumber',
      header: 'fiscal_failures.list.columns.receipt_number',
      width: '14%',
    },
    {
      id: 'orderNumber',
      field: 'orderNumber',
      header: 'fiscal_failures.list.columns.order_number',
      width: '12%',
      getValue: (row: FiscalFailureDto) => row.orderNumber || '-',
    },
    {
      id: 'issuedAt',
      field: 'issuedAt',
      header: 'fiscal_failures.list.columns.issued_at',
      width: '12%',
      getValue: (row: FiscalFailureDto) => {
        if (!row.issuedAt) return '';
        const date =
          row.issuedAt instanceof Date ? row.issuedAt : new Date(row.issuedAt);
        return date.toLocaleString('en-GB');
      },
    },
    {
      id: 'fiscalProviderKey',
      field: 'fiscalProviderKey',
      header: 'fiscal_failures.list.columns.provider',
      width: '10%',
      getValue: (row: FiscalFailureDto) => row.fiscalProviderKey || '-',
    },
    {
      id: 'errorKind',
      field: 'errorKind',
      header: 'fiscal_failures.list.columns.error_kind',
      width: '10%',
      customTemplate: errorKindTemplate,
    },
    {
      id: 'errorMessage',
      field: 'errorMessage',
      header: 'fiscal_failures.list.columns.error_message',
      width: '22%',
      getValue: (row: FiscalFailureDto) => row.errorMessage || '-',
    },
    {
      id: 'retryCount',
      field: 'retryCount',
      header: 'fiscal_failures.list.columns.retry_count',
      width: '8%',
      getValue: (row: FiscalFailureDto) => `${row.retryCount ?? 0}`,
    },
    {
      id: 'nextRetryAt',
      field: 'nextRetryAt',
      header: 'fiscal_failures.list.columns.next_retry_at',
      width: '12%',
      getValue: (row: FiscalFailureDto) => {
        if (!row.nextRetryAt) return translate.instant('fiscal_failures.list.no_retry');
        const date =
          row.nextRetryAt instanceof Date
            ? row.nextRetryAt
            : new Date(row.nextRetryAt);
        return date.toLocaleString('en-GB');
      },
    },
  ];
}

export function getFiscalFailureTableActions(
  defs: {
    onRetry: (row: FiscalFailureDto) => void;
    onAcknowledge: (row: FiscalFailureDto) => void;
  },
  translate: TranslateService
): TableAction<FiscalFailureDto>[] {
  return [
    {
      icon: 'pi pi-refresh',
      onClick: (row: FiscalFailureDto) => defs.onRetry(row),
      color: 'info',
      tooltip: translate.instant('fiscal_failures.list.retry_now'),
    },
    {
      icon: 'pi pi-check',
      onClick: (row: FiscalFailureDto) => defs.onAcknowledge(row),
      color: 'warning',
      tooltip: translate.instant('fiscal_failures.list.acknowledge'),
    },
  ];
}
