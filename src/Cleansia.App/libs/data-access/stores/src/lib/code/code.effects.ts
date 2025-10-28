import { Injectable, inject } from '@angular/core';
import { Actions, createEffect, ofType } from '@ngrx/effects';
import { Client } from '@cleansia/services';
import { of } from 'rxjs';
import { catchError, map, mergeMap } from 'rxjs/operators';
import * as CodeActions from './code.actions';

@Injectable()
export class CodeEffects {
  private readonly client = inject(Client);
  private readonly actions$ = inject(Actions);

  loadCodes$ = createEffect(() =>
    this.actions$.pipe(
      ofType(CodeActions.loadCodes),
      mergeMap(() =>
        this.client.codeClient.getOverview().pipe(
          map((data) => CodeActions.loadCodesSuccess({ data })),
          catchError((error) => of(CodeActions.loadCodesFailure({ error })))
        )
      )
    )
  );
}
