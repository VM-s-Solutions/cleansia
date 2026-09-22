import { ActionReducerMap } from '@ngrx/store';
import { AdminCodeEffects, codeReducer, CodeState } from './code';
import { loadingReducer, LoadingState } from './loading';

// Admin app state - only include what's needed, exclude partner-specific stores
export interface AdminAppState {
  loading: LoadingState;
  code: CodeState;
}

export const adminReducers: ActionReducerMap<AdminAppState> = {
  loading: loadingReducer,
  code: codeReducer,
};

export const adminEffects = [AdminCodeEffects];
