import { Page } from '@cleansia/models';
import { UserListItem } from '@cleansia/partner-services';

export const USER_FEATURE_KEY = 'user';

export interface UserState {
  page: Page<UserListItem>;
  userDetail?: UserListItem;

  loading: Record<string, boolean>;
  error: Record<string, string | null>;
}

export const userInitialState: UserState = {
  page: Page.create(),
  userDetail: undefined,
  loading: {},
  error: {},
};
