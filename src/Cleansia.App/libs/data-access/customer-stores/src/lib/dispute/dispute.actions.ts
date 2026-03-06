import {
  ApiException,
  DisputeDetails,
  DisputeListItem,
} from '@cleansia/partner-services';
import { createAction, props } from '@ngrx/store';

export const loadCustomerDisputes = createAction(
  '[Customer Dispute] Load Paged',
  props<{ offset?: number; limit?: number }>()
);
export const loadCustomerDisputesSuccess = createAction(
  '[Customer Dispute] Load Paged Success',
  props<{ data: DisputeListItem[]; total: number }>()
);
export const loadCustomerDisputesFailure = createAction(
  '[Customer Dispute] Load Paged Failure',
  props<{ error: ApiException }>()
);

export const loadCustomerDisputeDetail = createAction(
  '[Customer Dispute] Load Detail',
  props<{ disputeId: string }>()
);
export const loadCustomerDisputeDetailSuccess = createAction(
  '[Customer Dispute] Load Detail Success',
  props<{ dispute: DisputeDetails }>()
);
export const loadCustomerDisputeDetailFailure = createAction(
  '[Customer Dispute] Load Detail Failure',
  props<{ error: ApiException }>()
);
