import { Action, createReducer, on } from '@ngrx/store';
import {
  setCustomerLoadingOffAction,
  setCustomerLoadingOnAction,
} from './loading.actions';
import {
  customerLoadingInitialState,
  CustomerLoadingState,
} from './loading.state';

const _customerLoadingReducer = createReducer(
  customerLoadingInitialState,
  on(setCustomerLoadingOnAction, () => ({
    loading: true,
  })),
  on(setCustomerLoadingOffAction, () => customerLoadingInitialState)
);

export function customerLoadingReducer(
  state: CustomerLoadingState | undefined,
  action: Action
) {
  return _customerLoadingReducer(state, action);
}
