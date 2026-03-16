export const CUSTOMER_LOADING_FEATURE_KEY = 'customerLoading';

export interface CustomerLoadingState {
  loading: boolean;
}

export const customerLoadingInitialState: CustomerLoadingState = {
  loading: false,
};
