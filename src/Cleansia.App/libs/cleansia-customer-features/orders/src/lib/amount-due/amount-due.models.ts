import { MyReceivableDto } from '@cleansia/customer-services';

/** ReceivableKind's wire values; the customer client carries the kind only as a Code. */
enum ReceivableKind {
  CashCancellationFee = 1,
  Lockout = 2,
  UnpaidCash = 3,
  TopUp = 4,
}

const KIND_LABEL_KEYS: Readonly<Record<number, string>> = {
  [ReceivableKind.CashCancellationFee]: 'pages.amount_due.kind.cash_cancellation_fee',
  [ReceivableKind.Lockout]: 'pages.amount_due.kind.lockout',
  [ReceivableKind.UnpaidCash]: 'pages.amount_due.kind.unpaid_cash',
  [ReceivableKind.TopUp]: 'pages.amount_due.kind.top_up',
};

const UNKNOWN_KIND_LABEL_KEY = 'pages.amount_due.title';

export interface AmountDueRow {
  id: string;
  orderId: string;
  displayOrderNumber: string;
  kindLabelKey: string;
  amount: number;
  currencyCode: string;
}

export function toAmountDueRow(receivable: MyReceivableDto): AmountDueRow {
  return {
    id: receivable.id ?? '',
    orderId: receivable.orderId ?? '',
    displayOrderNumber: receivable.displayOrderNumber ?? '',
    kindLabelKey: KIND_LABEL_KEYS[receivable.kind?.value] ?? UNKNOWN_KIND_LABEL_KEY,
    amount: receivable.amount,
    currencyCode: receivable.currencyCode ?? '',
  };
}
