import { TemplateRef } from '@angular/core';
import { DisputeListItem, DisputeStatus } from '@cleansia/admin-services';
import { TableAction, TableColumn } from '@cleansia/components';
import { formatDate, formatMoney, localeFor } from '@cleansia/utils';
import { TranslateService } from '@ngx-translate/core';

export function getDisputeTableDefinition(
  defs: {
    onViewDetails: (row: DisputeListItem) => void;
  },
  translate: TranslateService,
  statusTemplate?: TemplateRef<DisputeListItem>,
  reasonTemplate?: TemplateRef<DisputeListItem>
): {
  columns: TableColumn<DisputeListItem>[];
  actions: TableAction<DisputeListItem>[];
} {
  return {
    columns: [
      {
        id: 'displayOrderNumber',
        field: 'displayOrderNumber',
        header: translate.instant('pages.disputes_management.columns.order_number'),
        sortable: true,
        width: '12%',
      },
      {
        id: 'customerName',
        field: 'customerName',
        header: translate.instant('pages.disputes_management.columns.customer_name'),
        sortable: true,
        width: '16%',
      },
      {
        id: 'customerEmail',
        field: 'customerEmail',
        header: translate.instant('pages.disputes_management.columns.customer_email'),
        width: '18%',
      },
      {
        id: 'reason',
        field: 'reason',
        header: translate.instant('pages.disputes_management.columns.reason'),
        width: '14%',
        customTemplate: reasonTemplate,
      },
      {
        id: 'status',
        field: 'status',
        header: translate.instant('pages.disputes_management.columns.status'),
        sortable: true,
        width: '12%',
        customTemplate: statusTemplate,
      },
      {
        id: 'refundAmount',
        field: 'refundAmount',
        header: translate.instant('pages.disputes_management.columns.refund_amount'),
        width: '10%',
        getValue: (row: DisputeListItem) =>
          row?.refundAmount == null
            ? '-'
            : formatMoney(row.refundAmount, null, localeFor(translate.currentLang), { fractionDigits: 2 }),
      },
      {
        id: 'createdOn',
        field: 'createdOn',
        header: translate.instant('pages.disputes_management.columns.created_on'),
        sortable: true,
        width: '12%',
        getValue: (row: DisputeListItem) => formatDate(row?.createdOn, translate.currentLang),
      },
    ],
    actions: [
      {
        icon: 'pi pi-eye',
        tooltip: translate.instant('pages.disputes_management.actions.view_details'),
        color: 'info',
        onClick: (row: DisputeListItem) => defs.onViewDetails(row),
      },
    ],
  };
}

export const DISPUTE_STATUS_LABEL_KEYS: Readonly<Record<number, string>> = {
  [DisputeStatus.Pending]: 'pages.disputes_management.status.pending',
  [DisputeStatus.UnderReview]: 'pages.disputes_management.status.under_review',
  [DisputeStatus.WaitingForResponse]:
    'pages.disputes_management.status.waiting_for_response',
  [DisputeStatus.Resolved]: 'pages.disputes_management.status.resolved',
  [DisputeStatus.Closed]: 'pages.disputes_management.status.closed',
  [DisputeStatus.Escalated]: 'pages.disputes_management.status.escalated',
};

export interface DisputeStatusOption {
  label: string;
  value: DisputeStatus;
}

export function buildDisputeStatusOptions(
  translate: TranslateService
): DisputeStatusOption[] {
  return [
    DisputeStatus.Pending,
    DisputeStatus.UnderReview,
    DisputeStatus.WaitingForResponse,
    DisputeStatus.Resolved,
    DisputeStatus.Closed,
    DisputeStatus.Escalated,
  ].map((value) => ({
    label: translate.instant(DISPUTE_STATUS_LABEL_KEYS[value]),
    value,
  }));
}

/**
 * Backend BusinessErrorMessage code -> i18n key. Mirrors the explicit
 * map used by the order-refund facade so we never depend on the snackbar's
 * best-effort normalization for money/dispute paths.
 */

