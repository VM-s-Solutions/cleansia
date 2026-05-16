import { createReducer, on } from '@ngrx/store';
import * as OrderActions from './order.actions';
import { orderInitialState, OrderListKey, OrderState } from './order.state';

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

const loadingKeyFor = (listKey: OrderListKey) => `paged:${listKey}`;

export const orderReducer = createReducer(
  orderInitialState,

  on(OrderActions.loadOrderPaged, (state, { listKey }) =>
    setFlag(state, loadingKeyFor(listKey ?? 'paged'), true),
  ),
  on(OrderActions.loadOrderPagedSuccess, (state, { listKey, page }) =>
    setFlag(
      {
        ...state,
        pages: {
          ...state.pages,
          [listKey]: state.pages[listKey].updateDataAndTotalAndPageNumberAndPageSize(
            page.data!,
            page.total,
            page.pageNumber,
            page.pageSize,
          ),
        },
      },
      loadingKeyFor(listKey),
      false,
    ),
  ),
  on(OrderActions.loadOrderPagedFailure, (state, { listKey, error }) =>
    setFlag(state, loadingKeyFor(listKey), false, error.message),
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