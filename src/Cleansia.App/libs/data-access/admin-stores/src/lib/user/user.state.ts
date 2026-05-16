import { Page } from '@cleansia/models';
import { AdminUserDetailDto, AdminUserListItem } from '@cleansia/admin-services';

export const USER_FEATURE_KEY = 'user';

export interface UserState {
  page: Page<AdminUserListItem>;
  userDetail?: AdminUserDetailDto;
  currentUser?: AdminUserListItem;

  loading: Record<string, boolean>;
  error: Record<string, string | null>;
}

export const userInitialState: UserState = {
  page: Page.create(),
  userDetail: undefined,
  currentUser: undefined,
  loading: {},
  error: {},
};
