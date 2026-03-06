import { createFeatureSelector, createSelector } from '@ngrx/store';
import {
  CUSTOMER_ORDER_FEATURE_KEY,
  CustomerOrderState,
} from './order.state';

export const selectCustomerOrderState =
  createFeatureSelector<CustomerOrderState>(CUSTOMER_ORDER_FEATURE_KEY);

export const selectCustomerOrders = createSelector(
  selectCustomerOrderState,
  (state: CustomerOrderState) => state.orders
);

export const selectCustomerOrdersTotal = createSelector(
  selectCustomerOrderState,
  (state: CustomerOrderState) => state.totalRecords
);

export const selectCustomerOrderDetail = createSelector(
  selectCustomerOrderState,
  (state: CustomerOrderState) => state.orderDetail
);

export const selectCustomerOrderLoading = (key: string) =>
  createSelector(
    selectCustomerOrderState,
    (state: CustomerOrderState) => state.loading[key] ?? false
  );

export const selectCustomerOrderError = (key: string) =>
  createSelector(
    selectCustomerOrderState,
    (state: CustomerOrderState) => state.error[key] ?? null
  );
