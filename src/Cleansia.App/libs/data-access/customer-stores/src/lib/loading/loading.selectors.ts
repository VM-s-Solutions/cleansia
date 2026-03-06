import { createFeatureSelector, createSelector } from '@ngrx/store';
import {
  CUSTOMER_LOADING_FEATURE_KEY,
  CustomerLoadingState,
} from './loading.state';

export const selectCustomerLoadingState =
  createFeatureSelector<CustomerLoadingState>(CUSTOMER_LOADING_FEATURE_KEY);

export const selectCustomerLoading = createSelector(
  selectCustomerLoadingState,
  (state: CustomerLoadingState) => state.loading
);
