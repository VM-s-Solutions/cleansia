import { inject, Injectable } from '@angular/core';
import { Actions, createEffect, ofType } from '@ngrx/effects';
import { catchError, map, of } from 'rxjs';
import * as UserActions from './user.actions';

@Injectable()
export class AdminUserEffects {
  private readonly actions$ = inject(Actions);

  // Simplified loadCurrent effect - returns success immediately
  // This can be expanded later when admin user management is needed
  loadCurrent$ = createEffect(() =>
    this.actions$.pipe(
      ofType(UserActions.loadUserCurrent),
      map(() => {
        // For now, just return success with empty user
        // The actual user data will be in the JWT token
        return UserActions.loadUserCurrentSuccess({ user: {} as any });
      }),
      catchError((error) => of(UserActions.loadUserCurrentFailure({ error })))
    )
  );
}
