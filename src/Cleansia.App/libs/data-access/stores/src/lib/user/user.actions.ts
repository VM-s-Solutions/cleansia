import { HttpErrorResponse } from '@angular/common/http';
import { UserFilter } from '@cleansia/models';
import {
  ApiException,
  BlobFileDto,
  SortDefinition,
  UserListItem,
  UserListItemPagedData,
} from '@cleansia/services';

import { createAction, props } from '@ngrx/store';

export const loadUserPaged = createAction(
  '[User] Load Paged',
  props<{
    filter?: UserFilter;
    isActive?: boolean;
    sort?: SortDefinition[];
    offset?: number;
    limit?: number;
  }>()
);
export const loadUserPagedSuccess = createAction(
  '[User] Load Paged Success',
  props<{ page: UserListItemPagedData }>()
);
export const loadUserPagedFailure = createAction(
  '[User] Load Paged Failure',
  props<{ error: ApiException }>()
);

export const loadUserCurrent = createAction('[User] Load Current');
export const loadUserCurrentSuccess = createAction(
  '[User] Load Current Success',
  props<{ user: UserListItem }>()
);
export const loadUserCurrentFailure = createAction(
  '[User] Load Current Failure',
  props<{ error: ApiException }>()
);

export const loadUserDetail = createAction(
  '[User] Load Detail',
  props<{ id: string }>()
);
export const loadUserDetailSuccess = createAction(
  '[User] Load Detail Success',
  props<{ user: UserListItem }>()
);
export const loadUserDetailFailure = createAction(
  '[User] Load Detail Failure',
  props<{ error: ApiException }>()
);

export const updateUserCurrent = createAction(
  '[User] Update Current',
  props<{
    id: string;
    firstName: string;
    lastName: string;
    phoneNumber?: string;
    birthDate?: Date;
    photo?: BlobFileDto;
  }>()
);
export const updateUserCurrentSuccess = createAction(
  '[User] Update Current Success',
  props<{ id: string }>()
);
export const updateUserCurrentFailure = createAction(
  '[User] Update Current Failure',
  props<{ error: ApiException }>()
);

export const logout = createAction('[User] Logout');
export const logoutSuccess = createAction('[User] Logout Success');
export const logoutFailure = createAction(
  '[User] Logout Failure',
  props<{ error: HttpErrorResponse }>()
);
