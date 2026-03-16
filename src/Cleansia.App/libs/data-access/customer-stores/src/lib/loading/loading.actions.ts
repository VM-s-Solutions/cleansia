import { createAction } from '@ngrx/store';

export const setCustomerLoadingOnAction = createAction(
  '[Customer Loading] Set Loading On'
);

export const setCustomerLoadingOffAction = createAction(
  '[Customer Loading] Set Loading Off'
);
