import { Code } from '@cleansia/admin-services';
import * as AdminCodeActions from './admin-code.actions';
import { codeReducer } from './code.reducers';
import { codeInitialState } from './code.state';

const CODES = [Code.fromJS({ type: 'OrderStatus', name: 'New', value: 0 })];

describe('codeReducer', () => {
  it('starts empty, not loading and not errored — the three states are distinguishable', () => {
    expect(codeReducer(undefined, { type: '@@init' })).toEqual({
      data: [],
      loading: false,
      error: null,
    });
  });

  it('enters loading and clears a previous error, so a retry does not render both at once', () => {
    const errored = { ...codeInitialState, error: 'offline' };

    expect(codeReducer(errored, AdminCodeActions.loadAdminCodes())).toEqual({
      data: [],
      loading: true,
      error: null,
    });
  });

  it('leaves loading and holds the codes on success', () => {
    const loading = { ...codeInitialState, loading: true };

    expect(
      codeReducer(loading, AdminCodeActions.loadAdminCodesSuccess({ data: CODES })),
    ).toEqual({ data: CODES, loading: false, error: null });
  });

  it('leaves loading and records the error on failure', () => {
    const loading = { ...codeInitialState, loading: true };

    expect(
      codeReducer(loading, AdminCodeActions.loadAdminCodesFailure({ error: 'offline' })),
    ).toEqual({ data: [], loading: false, error: 'offline' });
  });

  it('substitutes a message when the failure carries a blank one, so the error state is never silent', () => {
    const state = codeReducer(
      codeInitialState,
      AdminCodeActions.loadAdminCodesFailure({ error: '' }),
    );

    expect(state.error).toBe('Failed to load codes');
    expect(state.loading).toBe(false);
  });

  it('keeps the previously loaded codes when a refresh fails', () => {
    const loaded = { data: CODES, loading: true, error: null };

    expect(
      codeReducer(loaded, AdminCodeActions.loadAdminCodesFailure({ error: 'offline' })).data,
    ).toEqual(CODES);
  });

  it('does not mutate the state it was given', () => {
    const before = { ...codeInitialState };

    codeReducer(before, AdminCodeActions.loadAdminCodesSuccess({ data: CODES }));

    expect(before).toEqual({ data: [], loading: false, error: null });
  });
});
