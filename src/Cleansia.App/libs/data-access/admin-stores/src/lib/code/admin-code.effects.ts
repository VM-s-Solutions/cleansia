import { Injectable, inject } from '@angular/core';
import { AdminClient } from '@cleansia/admin-services';
import { Actions, createEffect, ofType } from '@ngrx/effects';
import { of } from 'rxjs';
import { catchError, map, mergeMap } from 'rxjs/operators';
import * as AdminCodeActions from './admin-code.actions';

@Injectable()
export class AdminCodeEffects {
  private readonly adminClient = inject(AdminClient);
  private readonly actions$ = inject(Actions);

  loadAdminCodes$ = createEffect(() =>
    this.actions$.pipe(
      ofType(AdminCodeActions.loadAdminCodes),
      mergeMap(() =>
        this.adminClient.adminCodeClient.getOverview().pipe(
          // `?? []` at the payload boundary, because the reducer stores what the action carries —
          // it spreads `data` straight into state with no default of its own. The generated client
          // answers a 200 whose body is not a JSON array (and a 204) with NULL rather than an empty
          // list, from the `result200 = null as any` branch its declared `Code[]` return type
          // denies, and `catchError` never fires on it because null is not an error. These codes
          // back every enum dropdown in the admin app, so a null in the store empties all of them.
          map((data) =>
            AdminCodeActions.loadAdminCodesSuccess({ data: data ?? [] })
          ),
          catchError((error) =>
            of(AdminCodeActions.loadAdminCodesFailure({ error }))
          )
        )
      )
    )
  );
}
