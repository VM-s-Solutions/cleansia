import { createReducer, on } from '@ngrx/store';
import * as CustomerUserActions from './user.actions';
import { customerUserInitialState, CustomerUserState } from './user.state';

const setFlag = (
  state: CustomerUserState,
  key: string,
  loading: boolean,
  error?: string
) => ({
  ...state,
  loading: { ...state.loading, [key]: loading },
  error: { ...state.error, [key]: error ?? null },
});

export const customerUserReducer = createReducer(
  customerUserInitialState,

  on(CustomerUserActions.loadCustomerUser, (state) =>
    setFlag(state, 'current', true)
  ),
  on(CustomerUserActions.loadCustomerUserSuccess, (state, { user }) =>
    setFlag({ ...state, currentUser: user }, 'current', false)
  ),
  on(CustomerUserActions.loadCustomerUserFailure, (state, { error }) =>
    setFlag(state, 'current', false, error.message)
  ),

  on(CustomerUserActions.customerLogout, (state) =>
    setFlag(state, 'logout', true)
  ),
  on(CustomerUserActions.customerLogoutSuccess, () => customerUserInitialState)
);
