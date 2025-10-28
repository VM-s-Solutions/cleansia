import { Action, createReducer, on } from '@ngrx/store';
import * as CodeActions from './code.actions';
import { codeInitialState, CodeState } from './code.state';

const _codeReducer = createReducer(
  codeInitialState,
  on(CodeActions.loadCodes, (state) => ({
    ...state,
    loading: true,
    error: null,
  })),
  on(CodeActions.loadCodesSuccess, (state, { data }) => ({
    ...state,
    data,
    loading: false,
    error: null,
  })),
  on(CodeActions.loadCodesFailure, (state, { error }) => ({
    ...state,
    loading: false,
    error: error.message || 'Failed to load codes',
  }))
);

export function codeReducer(state: CodeState | undefined, action: Action) {
  return _codeReducer(state, action);
}
