import { createFeatureSelector, createSelector } from '@ngrx/store';
import { LOADING_FEATURE_KEY, LoadingState } from './loading.state';

export const selectLoadingState =
  createFeatureSelector<LoadingState>(LOADING_FEATURE_KEY);

export const selectLoading = createSelector(
  selectLoadingState,
  (state: LoadingState) => state.loading,
);
