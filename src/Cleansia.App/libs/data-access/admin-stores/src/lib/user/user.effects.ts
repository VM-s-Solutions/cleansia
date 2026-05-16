import { inject, Injectable } from '@angular/core';
import { AdminUserListItem } from '@cleansia/admin-services';
import { Actions, createEffect, ofType } from '@ngrx/effects';
import { catchError, map, of } from 'rxjs';
import * as UserActions from './user.actions';

@Injectable()
export class AdminUserEffects {
  private readonly actions$ = inject(Actions);

  // Until the admin "current user" endpoint is wired, the JWT carries the
  // identity for the running session — emit an empty record so reducers
  // unblock without making a network call.
  loadCurrent$ = createEffect(() =>
    this.actions$.pipe(
      ofType(UserActions.loadUserCurrent),
      map(() =>
        UserActions.loadUserCurrentSuccess({ user: new AdminUserListItem() })
      ),
      catchError((error) => of(UserActions.loadUserCurrentFailure({ error })))
    )
  );
}
