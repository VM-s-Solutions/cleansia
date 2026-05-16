import {
  ApiException,
  CreateOrderResponse,
  OrderItem,
  OrderListItem,
  OrderStatus,
  PagedDataOfOrderListItem,
  PaymentStatus,
  PaymentType,
  SortDefinition,
} from '@cleansia/customer-services';
import { createAction, props } from '@ngrx/store';

export const loadCustomerOrders = createAction(
  '[Customer Order] Load Paged',
  props<{
    orderStatuses?: OrderStatus[];
    paymentStatuses?: PaymentStatus[];
    paymentTypes?: PaymentType[];
    cleaningDateFrom?: Date;
    cleaningDateTo?: Date;
    sort?: SortDefinition[];
    offset?: number;
    limit?: number;
  }>()
);
export const loadCustomerOrdersSuccess = createAction(
  '[Customer Order] Load Paged Success',
  props<{ data: OrderListItem[]; total: number }>()
);
export const loadCustomerOrdersFailure = createAction(
  '[Customer Order] Load Paged Failure',
  props<{ error: ApiException }>()
);

export const loadCustomerOrderDetail = createAction(
  '[Customer Order] Load Detail',
  props<{ orderId: string }>()
);
export const loadCustomerOrderDetailSuccess = createAction(
  '[Customer Order] Load Detail Success',
  props<{ order: OrderItem }>()
);
export const loadCustomerOrderDetailFailure = createAction(
  '[Customer Order] Load Detail Failure',
  props<{ error: ApiException }>()
);

export const clearCustomerOrderState = createAction('[Customer Order] Clear State');
