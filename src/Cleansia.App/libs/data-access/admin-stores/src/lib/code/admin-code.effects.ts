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
          map((data) => AdminCodeActions.loadAdminCodesSuccess({ data })),
          catchError((error) =>
            of(AdminCodeActions.loadAdminCodesFailure({ error }))
          )
        )
      )
    )
  );
}
