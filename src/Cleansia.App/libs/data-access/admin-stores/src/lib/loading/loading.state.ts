import { StateAdapter } from '@cleansia/models';

export const LOADING_FEATURE_KEY = 'loading';

export interface LoadingState {
  loading?: boolean;
}

export const loadingInitialState: LoadingState = {
  loading: false,
};

export const loadingStateAdapter = new StateAdapter<LoadingState>(
  loadingInitialState
);
