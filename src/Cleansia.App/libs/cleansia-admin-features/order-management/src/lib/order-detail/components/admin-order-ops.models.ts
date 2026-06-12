import { OrderStatus } from '@cleansia/admin-services';

export type AdminOrderOpsPanel =
  | 'cancel'
  | 'overrideStatus'
  | 'reassign'
  | 'refund';

export interface OrderStatusOption {
  value: OrderStatus;
  labelKey: string;
}

export const OVERRIDE_STATUS_OPTIONS: ReadonlyArray<OrderStatusOption> = [
  { value: OrderStatus.New, labelKey: 'pages.order_management.order_status.new' },
  {
    value: OrderStatus.Pending,
    labelKey: 'pages.order_management.order_status.pending',
  },
  {
    value: OrderStatus.Confirmed,
    labelKey: 'pages.order_management.order_status.confirmed',
  },
  {
    value: OrderStatus.OnTheWay,
    labelKey: 'pages.order_management.order_status.on_the_way',
  },
  {
    value: OrderStatus.InProgress,
    labelKey: 'pages.order_management.order_status.in_progress',
  },
  {
    value: OrderStatus.Completed,
    labelKey: 'pages.order_management.order_status.completed',
  },
  {
    value: OrderStatus.Cancelled,
    labelKey: 'pages.order_management.order_status.cancelled',
  },
];

export const ORDER_OPS_ERROR_KEY_MAP: Readonly<Record<string, string>> = {
  'order.not_found': 'errors.order.not_found',
  'order.already_cancelled': 'errors.order.already_cancelled',
  'order.already_completed': 'errors.order.already_completed',
  'order.in_progress_cannot_cancel': 'errors.order.in_progress_cannot_cancel',
  'order.invalid_status_transition': 'errors.order.invalid_status_transition',
  'order.no_available_spots': 'errors.order.no_available_spots',
  'order.employee_already_assigned': 'errors.order.employee_already_assigned',
  'order.employee_not_assigned': 'errors.order.employee_not_assigned',
  'employee.not_found': 'errors.employee.not_found',
  'refund.order_not_refundable': 'errors.refund.order_not_refundable',
  'refund.failed': 'errors.refund.failed',
  'common.required': 'errors.common.required',
  'common.max_length': 'errors.common.max_length',
  'common.invalid_enum_value': 'errors.common.invalid_enum_value',
};

export const ORDER_OPS_FALLBACK_ERROR_KEY = 'errors.common.error_occurred';
