import { createFeatureSelector, createSelector } from '@ngrx/store';
import {
  CUSTOMER_DISPUTE_FEATURE_KEY,
  CustomerDisputeState,
} from './dispute.state';

export const selectCustomerDisputeState =
  createFeatureSelector<CustomerDisputeState>(CUSTOMER_DISPUTE_FEATURE_KEY);

export const selectCustomerDisputes = createSelector(
  selectCustomerDisputeState,
  (state) => state.disputes
);

export const selectCustomerDisputesTotal = createSelector(
  selectCustomerDisputeState,
  (state) => state.totalRecords
);

export const selectCustomerDisputeDetail = createSelector(
  selectCustomerDisputeState,
  (state) => state.disputeDetail
);

export const selectCustomerDisputeLoading = (key: string) =>
  createSelector(
    selectCustomerDisputeState,
    (state) => state.loading[key] ?? false
  );
