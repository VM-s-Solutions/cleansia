import { createFeatureSelector, createSelector } from '@ngrx/store';
import { USER_FEATURE_KEY, UserState } from './user.state';

export const selectUserState =
  createFeatureSelector<UserState>(USER_FEATURE_KEY);

export const selectUserPage = createSelector(selectUserState, (s) => s.page);
export const selectUserItems = createSelector(
  selectUserPage,
  (page) => page?.data,
);
export const selectUserDetail = createSelector(
  selectUserState,
  (s) => s.userDetail,
);
export const selectCurrentUser = createSelector(
  selectUserState,
  (s) => s.currentUser,
);

export const selectUserLoading = (key: string) =>
  createSelector(selectUserState, (s) => s.loading[key] ?? false);

export const selectUserError = (key: string) =>
  createSelector(selectUserState, (s) => s.error[key] ?? null);
