import { Page } from '@cleansia/models';
import { OrderItem, OrderListItem } from '@cleansia/partner-services';

export const ORDER_FEATURE_KEY = 'order';

export interface OrderState {
  page: Page<OrderListItem>;
  orderDetail?: OrderItem;

  loading: Record<string, boolean>;
  error: Record<string, string | null>;
}

export const orderInitialState: OrderState = {
  page: Page.create(),
  orderDetail: undefined,
  loading: {},
  error: {},
};
