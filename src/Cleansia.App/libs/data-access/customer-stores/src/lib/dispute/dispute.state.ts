import { DisputeDetails, DisputeListItem } from '@cleansia/customer-services';

export const CUSTOMER_DISPUTE_FEATURE_KEY = 'customerDispute';

export interface CustomerDisputeState {
  disputes: DisputeListItem[];
  totalRecords: number;
  disputeDetail?: DisputeDetails;
  loading: Record<string, boolean>;
}

export const customerDisputeInitialState: CustomerDisputeState = {
  disputes: [],
  totalRecords: 0,
  loading: {},
};
