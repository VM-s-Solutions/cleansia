import { HttpErrorResponse } from '@angular/common/http';
import { OrderFilter } from '@cleansia/models';
import {
  ApiException,
  OrderListItem,
  OrderListItemPagedData,
  SortDefinition,
} from '@cleansia/services';

import { createAction, props } from '@ngrx/store';

export const loadOrderPaged = createAction(
  '[Order] Load Paged',
  props<{
    filter?: OrderFilter;
    isActive?: boolean;
    sort?: SortDefinition[];
    offset?: number;
    limit?: number;
  }>()
);
export const loadOrderPagedSuccess = createAction(
  '[Order] Load Paged Success',
  props<{ page: OrderListItemPagedData }>()
);
export const loadOrderPagedFailure = createAction(
  '[Order] Load Paged Failure',
  props<{ error: ApiException }>()
);

export const loadOrderDetail = createAction(
  '[Order] Load Detail',
  props<{ id: string }>()
);
export const loadOrderDetailSuccess = createAction(
  '[Order] Load Detail Success',
  props<{ order: OrderListItem }>()
);
export const loadOrderDetailFailure = createAction(
  '[Order] Load Detail Failure',
  props<{ error: ApiException }>()
);

export const clearOrderState = createAction('[Order] Clear State');