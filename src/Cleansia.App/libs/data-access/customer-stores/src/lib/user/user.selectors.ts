import { createFeatureSelector, createSelector } from '@ngrx/store';
import {
  CUSTOMER_USER_FEATURE_KEY,
  CustomerUserState,
} from './user.state';

export const selectCustomerUserState =
  createFeatureSelector<CustomerUserState>(CUSTOMER_USER_FEATURE_KEY);

export const selectCustomerCurrentUser = createSelector(
  selectCustomerUserState,
  (state: CustomerUserState) => state.currentUser
);

export const selectCustomerUserLoading = createSelector(
  selectCustomerUserState,
  (state: CustomerUserState) => state.loading['current'] ?? false
);
