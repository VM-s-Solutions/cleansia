import { createReducer, on } from '@ngrx/store';
import * as OrderActions from './order.actions';
import { orderInitialState, OrderState } from './order.state';

const setFlag = (
  state: OrderState,
  key: string,
  loading: boolean,
  error?: string,
) => ({
  ...state,
  loading: { ...state.loading, [key]: loading },
  error: { ...state.error, [key]: error ?? null },
});

export const orderReducer = createReducer(
  orderInitialState,

  on(OrderActions.loadOrderPaged, (state) => setFlag(state, 'paged', true)),
  on(OrderActions.loadOrderPagedSuccess, (state, { page }) =>
    setFlag(
      {
        ...state,
        page: state.page.updateDataAndTotalAndPageNumberAndPageSize(
          page.data!,
          page.total,
          page.pageNumber,
          page.pageSize,
        ),
      },
      'paged',
      false,
    ),
  ),
  on(OrderActions.loadOrderPagedFailure, (state, { error }) =>
    setFlag(state, 'paged', false, error.message),
  ),

  on(OrderActions.loadOrderDetail, (state) => setFlag(state, 'detail', true)),
  on(OrderActions.loadOrderDetailSuccess, (state, { order }) =>
    setFlag({ ...state, orderDetail: order }, 'detail', false),
  ),
  on(OrderActions.loadOrderDetailFailure, (state, { error }) =>
    setFlag(state, 'detail', false, error.message),
  ),

  on(OrderActions.clearOrderState, () => orderInitialState),
);