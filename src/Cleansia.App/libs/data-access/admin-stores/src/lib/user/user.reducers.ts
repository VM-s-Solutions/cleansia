import { createReducer, on } from '@ngrx/store';
import * as UserActions from './user.actions';
import { userInitialState, UserState } from './user.state';

const setFlag = (
  state: UserState,
  key: string,
  loading: boolean,
  error?: string,
) => ({
  ...state,
  loading: { ...state.loading, [key]: loading },
  error: { ...state.error, [key]: error ?? null },
});

export const userReducer = createReducer(
  userInitialState,

  on(UserActions.loadUserPaged, (state) => setFlag(state, 'paged', true)),
  on(UserActions.loadUserPagedSuccess, (state, { page }) =>
    setFlag(
      {
        ...state,
        page: state.page.updateDataAndTotalAndPageNumberAndPageSize(
          page.data!,
          page.total,
          page.pageNumber,
          page.pageSize,
        ),
      },
      'paged',
      false,
    ),
  ),
  on(UserActions.loadUserPagedFailure, (state, { error }) =>
    setFlag(state, 'paged', false, error.message),
  ),

  on(UserActions.loadUserCurrent, (state) => setFlag(state, 'current', true)),
  on(UserActions.loadUserCurrentSuccess, (state, { user }) =>
    setFlag({ ...state, userDetail: user }, 'current', false),
  ),
  on(UserActions.loadUserCurrentFailure, (state, { error }) =>
    setFlag(state, 'current', false, error.message),
  ),

  on(UserActions.loadUserDetail, (state) => setFlag(state, 'detail', true)),
  on(UserActions.loadUserDetailSuccess, (state, { user }) =>
    setFlag({ ...state, userDetail: user }, 'detail', false),
  ),
  on(UserActions.loadUserDetailFailure, (state, { error }) =>
    setFlag(state, 'detail', false, error.message),
  ),

  on(UserActions.updateUserCurrent, (state) =>
    setFlag(state, 'updateCurrent', true),
  ),
  on(UserActions.updateUserCurrentSuccess, (state) =>
    setFlag(state, 'updateCurrent', false),
  ),
  on(UserActions.updateUserCurrentFailure, (state, { error }) =>
    setFlag(state, 'updateCurrent', false, error.message),
  ),

  on(UserActions.logout, (state) => setFlag(state, 'logout', true)),
  on(UserActions.logoutSuccess, () => userInitialState),
  on(UserActions.logoutFailure, (state, { error }) =>
    setFlag(state, 'logout', false, error.message),
  ),
);
