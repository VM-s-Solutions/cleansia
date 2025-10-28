import { createAction, props } from '@ngrx/store';
import { ApiException, Code } from '@cleansia/services';

// Load Codes
export const loadCodes = createAction('[Code] Load Codes');

export const loadCodesSuccess = createAction(
  '[Code] Load Codes Success',
  props<{ data: Code[] }>()
);

export const loadCodesFailure = createAction(
  '[Code] Load Codes Failure',
  props<{ error: ApiException }>()
);
