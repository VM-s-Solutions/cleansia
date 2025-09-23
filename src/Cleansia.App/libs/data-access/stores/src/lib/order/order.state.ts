import { Page } from '@cleansia/models';
import { OrderListItem } from '@cleansia/services';

export const ORDER_FEATURE_KEY = 'order';

export interface OrderState {
  page: Page<OrderListItem>;
  orderDetail?: OrderListItem;

  loading: Record<string, boolean>;
  error: Record<string, string | null>;
}

export const orderInitialState: OrderState = {
  page: Page.create(),
  orderDetail: undefined,
  loading: {},
  error: {},
};