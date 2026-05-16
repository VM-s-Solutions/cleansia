import { Page } from '@cleansia/models';
import { OrderItem, OrderListItem } from '@cleansia/partner-services';

export const ORDER_FEATURE_KEY = 'order';

/**
 * Discriminates between the partner Orders page's two simultaneously-rendered
 * lists (Available vs My). Both used to share a single `page` slice, so the
 * second load to fire would clobber the first list's data — express orders
 * appearing in My instead of Available, "Take Order" showing on completed
 * rows, etc. Per-list slices keep them independent.
 *
 * `'paged'` is kept as a generic catch-all bucket for any other order-paged
 * call sites that don't need the available/my split.
 */
export type OrderListKey = 'available' | 'my' | 'paged';

export interface OrderState {
  pages: Record<OrderListKey, Page<OrderListItem>>;
  orderDetail?: OrderItem;

  loading: Record<string, boolean>;
  error: Record<string, string | null>;
}

export const orderInitialState: OrderState = {
  pages: {
    available: Page.create(),
    my: Page.create(),
    paged: Page.create(),
  },
  orderDetail: undefined,
  loading: {},
  error: {},
};
