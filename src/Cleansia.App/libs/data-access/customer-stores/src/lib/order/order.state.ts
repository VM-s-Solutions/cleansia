import { OrderItem, OrderListItem } from '@cleansia/partner-services';

export const CUSTOMER_ORDER_FEATURE_KEY = 'customerOrder';

export interface CustomerOrderState {
  orders: OrderListItem[];
  totalRecords: number;
  orderDetail?: OrderItem;
  loading: Record<string, boolean>;
  error: Record<string, string | null>;
}

export const customerOrderInitialState: CustomerOrderState = {
  orders: [],
  totalRecords: 0,
  loading: {},
  error: {},
};
