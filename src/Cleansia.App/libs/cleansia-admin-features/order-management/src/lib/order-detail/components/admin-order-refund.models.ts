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

export const REFUND_FALLBACK_ERROR_KEY = 'api.refund.failed';
