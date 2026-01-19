import { Code } from '@cleansia/admin-services';
import { createAction, props } from '@ngrx/store';

export const loadAdminCodes = createAction('[Admin Code] Load Codes');

export const loadAdminCodesSuccess = createAction(
  '[Admin Code] Load Codes Success',
  props<{ data: Code[] }>()
);

export const loadAdminCodesFailure = createAction(
  '[Admin Code] Load Codes Failure',
  props<{ error: string }>()
);
