import { RefundReason } from '@cleansia/admin-services';

export type RefundLineKind = 'service' | 'package' | 'bundled';

export interface RefundLineOption {
  kind: RefundLineKind;
  id: string;
  name: string;
  price: number | null;
  selected: boolean;
  packageId?: string;
}

export interface RefundLineGroup {
  packageId: string;
  packageName: string;
  lines: RefundLineOption[];
}

export interface RefundReasonOption {
  label: string;
  value: RefundReason;
}

export const REFUND_REASON_OPTIONS: ReadonlyArray<{
  value: RefundReason;
  labelKey: string;
}> = [
  {
    value: RefundReason.CustomerCancellation,
    labelKey: 'pages.order_management.refund.reasons.customer_cancellation',
  },
  {
    value: RefundReason.DisputeResolution,
    labelKey: 'pages.order_management.refund.reasons.dispute_resolution',
  },
  {
    value: RefundReason.AdminDiscretion,
    labelKey: 'pages.order_management.refund.reasons.admin_discretion',
  },
  {
    value: RefundReason.ServiceNotRendered,
    labelKey: 'pages.order_management.refund.reasons.service_not_rendered',
  },
];

export const REFUND_ERROR_KEY_MAP: Readonly<Record<string, string>> = {
  'refund.lines_required': 'errors.refund.lines_required',
  'refund.line_invalid': 'errors.refund.line_invalid',
  'refund.override_reason_required': 'errors.refund.override_reason_required',
  'refund.failed': 'errors.refund.failed',
  'refund.nothing_refundable': 'errors.refund.nothing_refundable',
  'refund.order_not_refundable': 'errors.refund.order_not_refundable',
};

export const REFUND_FALLBACK_ERROR_KEY = 'errors.refund.failed';
