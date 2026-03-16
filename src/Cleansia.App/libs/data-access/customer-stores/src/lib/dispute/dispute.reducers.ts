import { createReducer, on } from '@ngrx/store';
import * as DisputeActions from './dispute.actions';
import { customerDisputeInitialState } from './dispute.state';

export const customerDisputeReducer = createReducer(
  customerDisputeInitialState,
  on(DisputeActions.loadCustomerDisputes, (state) => ({
    ...state,
    loading: { ...state.loading, paged: true },
  })),
  on(DisputeActions.loadCustomerDisputesSuccess, (state, { data, total }) => ({
    ...state,
    disputes: data,
    totalRecords: total,
    loading: { ...state.loading, paged: false },
  })),
  on(DisputeActions.loadCustomerDisputesFailure, (state) => ({
    ...state,
    loading: { ...state.loading, paged: false },
  })),
  on(DisputeActions.loadCustomerDisputeDetail, (state) => ({
    ...state,
    loading: { ...state.loading, detail: true },
  })),
  on(DisputeActions.loadCustomerDisputeDetailSuccess, (state, { dispute }) => ({
    ...state,
    disputeDetail: dispute,
    loading: { ...state.loading, detail: false },
  })),
  on(DisputeActions.loadCustomerDisputeDetailFailure, (state) => ({
    ...state,
    loading: { ...state.loading, detail: false },
  }))
);
