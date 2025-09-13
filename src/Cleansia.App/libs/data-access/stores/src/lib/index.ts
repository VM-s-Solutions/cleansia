import { ActionReducerMap } from '@ngrx/store';
import { loadingReducer, LoadingState } from './loading';
import { UserEffects, userReducer, UserState } from './user';

export * from './loading';
export * from './user';

export interface AppState {
  user: UserState;
  loading: LoadingState;
}

export const reducers: ActionReducerMap<AppState> = {
  user: userReducer,
  loading: loadingReducer,
};

export const effects = [UserEffects];
