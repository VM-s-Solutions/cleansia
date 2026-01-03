import { Action, createReducer, on } from '@ngrx/store';
import * as AdminCodeActions from './admin-code.actions';
import { codeInitialState, CodeState } from './code.state';

const _codeReducer = createReducer(
  codeInitialState,
  on(AdminCodeActions.loadAdminCodes, (state) => ({
    ...state,
    loading: true,
    error: null,
  })),
  on(AdminCodeActions.loadAdminCodesSuccess, (state, { data }) => ({
    ...state,
    data,
    loading: false,
    error: null,
  })),
  on(AdminCodeActions.loadAdminCodesFailure, (state, { error }) => ({
    ...state,
    loading: false,
    error: error || 'Failed to load codes',
  }))
);

export function codeReducer(state: CodeState | undefined, action: Action) {
  return _codeReducer(state, action);
}
