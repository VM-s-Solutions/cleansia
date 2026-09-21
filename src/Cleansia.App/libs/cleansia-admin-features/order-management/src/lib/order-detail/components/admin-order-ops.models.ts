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

