import { createFeatureSelector, createSelector } from '@ngrx/store';
import { ORDER_FEATURE_KEY, OrderListKey, OrderState } from './order.state';

export const selectOrderState =
  createFeatureSelector<OrderState>(ORDER_FEATURE_KEY);

export const selectOrderPage = (listKey: OrderListKey = 'paged') =>
  createSelector(selectOrderState, (s) => s.pages[listKey]);

export const selectOrderItems = (listKey: OrderListKey = 'paged') =>
  createSelector(selectOrderPage(listKey), (page) => page?.data);

export const selectOrderTotal = (listKey: OrderListKey = 'paged') =>
  createSelector(selectOrderPage(listKey), (page) => page?.total ?? 0);

export const selectOrderDetail = createSelector(
  selectOrderState,
  (s) => s.orderDetail,
);

export const selectOrderLoading = (key: string) =>
  createSelector(selectOrderState, (s) => s.loading[key] ?? false);

export const selectOrderError = (key: string) =>
  createSelector(selectOrderState, (s) => s.error[key] ?? null);