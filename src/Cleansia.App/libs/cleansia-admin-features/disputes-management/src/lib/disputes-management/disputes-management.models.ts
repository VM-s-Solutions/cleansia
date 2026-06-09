import { TemplateRef } from '@angular/core';
import { DisputeListItem, DisputeStatus } from '@cleansia/admin-services';
import { TableAction, TableColumn } from '@cleansia/components';
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
          row?.refundAmount == null ? '-' : row.refundAmount.toFixed(2),
      },
      {
        id: 'createdOn',
        field: 'createdOn',
        header: translate.instant('pages.disputes_management.columns.created_on'),
        sortable: true,
        width: '12%',
        getValue: (row: DisputeListItem) => {
          if (!row?.createdOn) return '';
          const date =
            row.createdOn instanceof Date
              ? row.createdOn
              : new Date(row.createdOn);
          return date.toLocaleDateString('en-GB');
        },
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

export function getDisputeStatusClass(status: number | undefined | null): string {
  switch (status) {
    case DisputeStatus.Pending:
      return 'dispute-status-badge status-pending';
    case DisputeStatus.UnderReview:
      return 'dispute-status-badge status-under-review';
    case DisputeStatus.WaitingForResponse:
      return 'dispute-status-badge status-waiting';
    case DisputeStatus.Resolved:
      return 'dispute-status-badge status-resolved';
    case DisputeStatus.Closed:
      return 'dispute-status-badge status-closed';
    case DisputeStatus.Escalated:
      return 'dispute-status-badge status-escalated';
    default:
      return 'dispute-status-badge status-pending';
  }
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
export const DISPUTE_ERROR_KEY_MAP: Readonly<Record<string, string>> = {
  'dispute.already_resolved': 'errors.dispute.already_resolved',
  'dispute.invalid_status_transition':
    'errors.dispute.invalid_status_transition',
  'dispute.not_found': 'errors.dispute.not_found',
  'dispute.invalid_refund_amount': 'errors.dispute.invalid_refund_amount',
  'dispute.max_length_exceeded': 'errors.dispute.max_length_exceeded',
  'refund.failed': 'errors.refund.failed',
  'refund.order_not_refundable': 'errors.refund.order_not_refundable',
  'refund.nothing_refundable': 'errors.refund.nothing_refundable',
};

export const DISPUTE_FALLBACK_ERROR_KEY = 'errors.dispute.action_failed';
