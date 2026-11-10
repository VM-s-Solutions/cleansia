import {
  Code,
  RecordRefundRepaymentCommand,
  RefundListItem,
  RefundRepaymentMethod,
  RefundState,
  RetryRefundCommand,
} from '@cleansia/admin-services';
import { ICleansiaSelectOption, TableAction, TableColumn } from '@cleansia/components';
import { PermissionService, Policy } from '@cleansia/services';
import { formatDate, formatMoney, localeFor } from '@cleansia/utils';
import { TranslateService } from '@ngx-translate/core';

/** The state filter's own value for the three states that still need someone: the page opens on it. */
export const NEEDS_ACTION = 0;
export type RefundStateFilter = typeof NEEDS_ACTION | RefundState;
export const NEEDS_ACTION_STATES: readonly RefundState[] = [RefundState.NotSent, RefundState.Failed, RefundState.Bounced];

export const REFUND_STATE_LABEL_KEYS: Readonly<Record<RefundState, string>> = {
  [RefundState.NotSent]: 'enums.refund_state.not_sent',
  [RefundState.Refunded]: 'enums.refund_state.refunded',
  [RefundState.Failed]: 'enums.refund_state.failed',
  [RefundState.Bounced]: 'enums.refund_state.bounced',
  [RefundState.Repaid]: 'enums.refund_state.repaid',
  [RefundState.Closed]: 'enums.refund_state.closed',
};

/** Mirrors the server's RefundOrigin, which reaches the client only as a `Code`. */
export const REFUND_ORIGIN_LABEL_KEYS: Readonly<Record<number, string>> = {
  1: 'enums.refund_origin.cancellation',
  2: 'enums.refund_origin.dispute',
  3: 'enums.refund_origin.full_refund',
  4: 'enums.refund_origin.partial_refund',
};

export const REFUND_REPAYMENT_METHOD_LABEL_KEYS: Readonly<Record<RefundRepaymentMethod, string>> = {
  [RefundRepaymentMethod.BankTransfer]: 'enums.refund_repayment_method.bank_transfer',
  [RefundRepaymentMethod.Credit]: 'enums.refund_repayment_method.credit',
};

const CANCELLATION_ORIGIN = 1;

function codeLabel(keys: Readonly<Record<number, string>>, code: Code | undefined, translate: TranslateService): string {
  const key = code ? keys[code.value] : undefined;
  return key ? translate.instant(key) : (code?.name ?? '-');
}

export function refundOriginLabel(row: RefundListItem, translate: TranslateService): string {
  return codeLabel(REFUND_ORIGIN_LABEL_KEYS, row.origin, translate);
}

export function formatRefundAmount(row: RefundListItem, lang: string | undefined): string {
  return formatMoney(row.amount, row.currencyCode, localeFor(lang), { fractionDigits: 2 });
}

export function refundCustomerLabel(row: RefundListItem, translate: TranslateService): string {
  const name = row.customerName || '-';
  return row.hasAccount ? name : `${name} (${translate.instant('pages.refunds.guest')})`;
}

const isState = (row: RefundListItem, state: RefundState): boolean => row.state?.value === state;

export const isOwedRefund = (row: RefundListItem): boolean =>
  isState(row, RefundState.Failed) || isState(row, RefundState.Bounced);

export const isDisputeRefundNotSent = (row: RefundListItem): boolean =>
  !!row.disputeId && isState(row, RefundState.NotSent);

/** The one short line under a state that says who acts next, or how it was settled. */
export function refundStateHint(row: RefundListItem, translate: TranslateService): string {
  if (isState(row, RefundState.Repaid)) {
    return row.repaymentMethod?.value === RefundRepaymentMethod.Credit
      ? translate.instant('pages.refunds.hints.repaid_credit')
      : translate.instant('pages.refunds.hints.repaid_transfer', { reference: row.repaymentReference ?? '' });
  }
  if (isState(row, RefundState.NotSent) && row.canRetry && row.origin?.value === CANCELLATION_ORIGIN) {
    return translate.instant('pages.refunds.hints.retried_hourly');
  }
  if (isState(row, RefundState.NotSent) || isOwedRefund(row)) {
    return translate.instant('pages.refunds.hints.waiting_for_admin');
  }
  return '';
}

export function statesFor(filter: RefundStateFilter | null): RefundState[] | undefined {
  if (filter === null) return undefined;
  return filter === NEEDS_ACTION ? [...NEEDS_ACTION_STATES] : [filter];
}

export function buildRefundStateOptions(translate: TranslateService): ICleansiaSelectOption[] {
  return [
    { label: translate.instant('pages.refunds.filters.needs_action'), value: NEEDS_ACTION },
    ...Object.values(RefundState)
      .filter((value): value is RefundState => typeof value === 'number')
      .map((value) => ({ label: translate.instant(REFUND_STATE_LABEL_KEYS[value]), value })),
  ];
}

export function buildRetryRefundCommand(row: RefundListItem): RetryRefundCommand {
  const command = new RetryRefundCommand();
  command.orderId = row.orderId;
  command.refundId = row.id;
  return command;
}

export function buildRecordRefundRepaymentCommand(
  row: RefundListItem,
  method: RefundRepaymentMethod,
  reference: string | undefined
): RecordRefundRepaymentCommand {
  const command = new RecordRefundRepaymentCommand();
  command.orderId = row.orderId;
  command.refundId = row.id;
  command.method = method;
  command.reference = reference;
  return command;
}

export function getRefundsTableDefinition(
  defs: {
    onRetry: (row: RefundListItem) => void;
    onOpenDispute: (row: RefundListItem) => void;
    onRecordRepayment: (row: RefundListItem) => void;
    isBusy: () => boolean;
  },
  translate: TranslateService,
  permissions: PermissionService,
  stateTemplate: TableColumn<RefundListItem>['customTemplate']
): { columns: TableColumn<RefundListItem>[]; actions: TableAction<RefundListItem>[] } {
  return {
    columns: [
      {
        id: 'displayOrderNumber',
        field: 'displayOrderNumber',
        header: translate.instant('pages.refunds.columns.order'),
        width: '11%',
      },
      {
        id: 'customer',
        field: 'customerName',
        header: translate.instant('pages.refunds.columns.customer'),
        getValue: (row: RefundListItem) => refundCustomerLabel(row, translate),
        width: '17%',
      },
      {
        id: 'origin',
        field: 'origin',
        header: translate.instant('pages.refunds.columns.origin'),
        getValue: (row: RefundListItem) => refundOriginLabel(row, translate),
        width: '13%',
      },
      {
        id: 'amount',
        numeric: true,
        field: 'amount',
        header: translate.instant('pages.refunds.columns.amount'),
        sortable: true,
        getValue: (row: RefundListItem) => formatRefundAmount(row, translate.currentLang),
        width: '11%',
      },
      {
        id: 'state',
        field: 'state',
        header: translate.instant('pages.refunds.columns.state'),
        customTemplate: stateTemplate,
        width: '20%',
      },
      {
        id: 'requestedOn',
        numeric: true,
        field: 'requestedOn',
        header: translate.instant('pages.refunds.columns.requested_on'),
        sortable: true,
        getValue: (row: RefundListItem) => formatDate(row.requestedOn, translate.currentLang, 'dateTime'),
        width: '13%',
      },
      {
        id: 'lastChangedOn',
        numeric: true,
        field: 'lastChangedOn',
        header: translate.instant('pages.refunds.columns.last_changed_on'),
        getValue: (row: RefundListItem) => formatDate(row.lastChangedOn, translate.currentLang, 'dateTime'),
        width: '13%',
      },
    ],
    actions: [
      {
        icon: 'pi pi-replay',
        tooltip: translate.instant('pages.refunds.actions.retry'),
        color: 'primary',
        visible: (row: RefundListItem) => !!row.canRetry && permissions.hasPolicy(Policy.CanIssueRefund),
        disabled: () => defs.isBusy(),
        onClick: (row: RefundListItem) => defs.onRetry(row),
      },
      {
        icon: 'pi pi-comments',
        tooltip: translate.instant('pages.refunds.actions.open_dispute'),
        color: 'info',
        visible: (row: RefundListItem) => isDisputeRefundNotSent(row),
        onClick: (row: RefundListItem) => defs.onOpenDispute(row),
      },
      {
        icon: 'pi pi-wallet',
        tooltip: translate.instant('pages.refunds.actions.record_repayment'),
        color: 'warning',
        visible: (row: RefundListItem) => isOwedRefund(row) && permissions.hasPolicy(Policy.CanIssueRefund),
        disabled: () => defs.isBusy(),
        onClick: (row: RefundListItem) => defs.onRecordRepayment(row),
      },
    ],
  };
}
