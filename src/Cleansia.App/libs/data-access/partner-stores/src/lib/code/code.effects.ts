import { Injectable, inject } from '@angular/core';
import { PartnerClient } from '@cleansia/partner-services';
import { Actions, createEffect, ofType } from '@ngrx/effects';
import { of } from 'rxjs';
import { catchError, map, mergeMap } from 'rxjs/operators';
import * as CodeActions from './code.actions';

@Injectable()
export class CodeEffects {
  private readonly partnerClient = inject(PartnerClient);
  private readonly actions$ = inject(Actions);

  loadCodes$ = createEffect(() =>
    this.actions$.pipe(
      ofType(CodeActions.loadCodes),
      mergeMap(() =>
        this.partnerClient.codeClient.getOverview().pipe(
          map((data) => CodeActions.loadCodesSuccess({ data })),
          catchError((error) => of(CodeActions.loadCodesFailure({ error })))
        )
      )
    )
  );
}
