import { createReducer, on } from '@ngrx/store';
import * as OrderActions from './order.actions';
import { customerOrderInitialState } from './order.state';

export const customerOrderReducer = createReducer(
  customerOrderInitialState,
  on(OrderActions.loadCustomerOrders, (state) => ({
    ...state,
    loading: { ...state.loading, paged: true },
    error: { ...state.error, paged: null },
  })),
  on(OrderActions.loadCustomerOrdersSuccess, (state, { data, total }) => ({
    ...state,
    orders: data,
    totalRecords: total,
    loading: { ...state.loading, paged: false },
  })),
  on(OrderActions.loadCustomerOrdersFailure, (state, { error }) => ({
    ...state,
    loading: { ...state.loading, paged: false },
    error: { ...state.error, paged: error.message },
  })),
  on(OrderActions.loadCustomerOrderDetail, (state) => ({
    ...state,
    loading: { ...state.loading, detail: true },
    error: { ...state.error, detail: null },
  })),
  on(OrderActions.loadCustomerOrderDetailSuccess, (state, { order }) => ({
    ...state,
    orderDetail: order,
    loading: { ...state.loading, detail: false },
  })),
  on(OrderActions.loadCustomerOrderDetailFailure, (state, { error }) => ({
    ...state,
    loading: { ...state.loading, detail: false },
    error: { ...state.error, detail: error.message },
  })),
  on(OrderActions.clearCustomerOrderState, () => customerOrderInitialState)
);
