import { ApiException, UserListItem } from '@cleansia/partner-services';
import { createAction, props } from '@ngrx/store';

export const loadCustomerUser = createAction('[Customer User] Load Current');
export const loadCustomerUserSuccess = createAction(
  '[Customer User] Load Current Success',
  props<{ user: UserListItem }>()
);
export const loadCustomerUserFailure = createAction(
  '[Customer User] Load Current Failure',
  props<{ error: ApiException }>()
);

export const customerLogout = createAction('[Customer User] Logout');
export const customerLogoutSuccess = createAction(
  '[Customer User] Logout Success'
);
