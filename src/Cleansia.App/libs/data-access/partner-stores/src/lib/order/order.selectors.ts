import { createFeatureSelector, createSelector } from '@ngrx/store';
import { ORDER_FEATURE_KEY, OrderState } from './order.state';

export const selectOrderState =
  createFeatureSelector<OrderState>(ORDER_FEATURE_KEY);

export const selectOrderPage = createSelector(selectOrderState, (s) => s.page);
export const selectOrderItems = createSelector(
  selectOrderPage,
  (page) => page?.data,
);
export const selectOrderTotal = createSelector(
  selectOrderPage,
  (page) => page?.total ?? 0,
);
export const selectOrderDetail = createSelector(
  selectOrderState,
  (s) => s.orderDetail,
);

export const selectOrderLoading = (key: string) =>
  createSelector(selectOrderState, (s) => s.loading[key] ?? false);

export const selectOrderError = (key: string) =>
  createSelector(selectOrderState, (s) => s.error[key] ?? null);