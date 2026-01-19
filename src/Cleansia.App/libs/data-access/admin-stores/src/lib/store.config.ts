import { ActionReducerMap } from '@ngrx/store';
import { AdminCodeEffects, codeReducer, CodeState } from './code';
import { loadingReducer, LoadingState } from './loading';
import { AdminUserEffects, userReducer, UserState } from './user';

// Admin app state - only include what's needed, exclude partner-specific stores
export interface AdminAppState {
  loading: LoadingState;
  code: CodeState;
  user: UserState;
}

export const adminReducers: ActionReducerMap<AdminAppState> = {
  loading: loadingReducer,
  code: codeReducer,
  user: userReducer,
};

export const adminEffects = [AdminCodeEffects, AdminUserEffects];
