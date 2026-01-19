import { Action, createReducer, on } from '@ngrx/store';
import { setLoadingOffAction, setLoadingOnAction } from './loading.actions';
import {
  loadingInitialState,
  LoadingState,
  loadingStateAdapter,
} from './loading.state';

const _loadingReducer = createReducer(
  loadingInitialState,
  on(setLoadingOnAction, () => {
    return loadingStateAdapter.updateState({ loading: true });
  }),
  on(setLoadingOffAction, () => {
    return loadingInitialState;
  }),
);

export function loadingReducer(
  state: LoadingState | undefined,
  action: Action,
) {
  return _loadingReducer(state, action);
}
