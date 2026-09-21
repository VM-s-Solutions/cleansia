import { TemplateRef } from '@angular/core';
import { FiscalErrorKind, FiscalFailureDto } from '@cleansia/admin-services';
import { TableColumn, TableAction } from '@cleansia/components';
import { formatDate } from '@cleansia/utils';
import { TranslateService } from '@ngx-translate/core';

/**
 * FiscalErrorKind wire values — the backend serializes enums as ints
 * (None=0, Transient=1, Permanent=2, Configuration=3, Unknown=4); the
 * generated string enum lies about the runtime shape until the admin
 * client is regenerated.
 */
const FISCAL_ERROR_KIND_BADGES: Readonly<Record<number, string>> = {
  1: 'transient',
  2: 'permanent',
  3: 'configuration',
  4: 'unknown',
};

export function getFiscalErrorKindBadge(
  kind: FiscalErrorKind | number | undefined
): string | undefined {
  return FISCAL_ERROR_KIND_BADGES[Number(kind)];
}

// The shared status pill's tone per kind: a transient failure retries itself, a permanent or a
// configuration one needs an administrator, an unknown one is neither yet.
const FISCAL_ERROR_KIND_TONES: Readonly<Record<string, string>> = {
  transient: 'warning',
  permanent: 'danger',
  configuration: 'danger',
  unknown: 'neutral',
};

export function getFiscalErrorKindClass(kind: FiscalErrorKind | number | undefined): string {
  const badge = getFiscalErrorKindBadge(kind);
  return badge ? `status-badge status-badge--${FISCAL_ERROR_KIND_TONES[badge]}` : '';
}

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
      numeric: true,
      field: 'issuedAt',
      header: 'fiscal_failures.list.columns.issued_at',
      width: '12%',
      getValue: (row: FiscalFailureDto) => formatDate(row.issuedAt, translate.currentLang, 'dateTime'),
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
      align: 'center',
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
      numeric: true,
      field: 'retryCount',
      header: 'fiscal_failures.list.columns.retry_count',
      width: '8%',
      getValue: (row: FiscalFailureDto) => `${row.retryCount ?? 0}`,
    },
    {
      id: 'nextRetryAt',
      numeric: true,
      field: 'nextRetryAt',
      header: 'fiscal_failures.list.columns.next_retry_at',
      width: '12%',
      getValue: (row: FiscalFailureDto) =>
        formatDate(row.nextRetryAt, translate.currentLang, 'dateTime') ||
        translate.instant('fiscal_failures.list.no_retry'),
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
