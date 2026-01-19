import { inject, Injectable } from '@angular/core';
import { PartnerClient } from '@cleansia/partner-services';
import { SnackbarService } from '@cleansia/services';
import { Actions, createEffect, ofType } from '@ngrx/effects';
import { Store } from '@ngrx/store';
import { TranslateService } from '@ngx-translate/core';
import { catchError, map, mergeMap, of } from 'rxjs';
import * as EmployeeActions from './employee.actions';

@Injectable()
export class EmployeeEffects {
  private readonly partnerClient = inject(PartnerClient);
  private readonly actions$ = inject(Actions);
  private readonly store = inject(Store);
  private readonly translate = inject(TranslateService);
  private readonly snackbarService = inject(SnackbarService);

  checkCurrent$ = createEffect(() =>
    this.actions$.pipe(
      ofType(EmployeeActions.checkEmployeeCurrent),
      mergeMap(() =>
        this.partnerClient.employeeClient.checkCurrentEmployee().pipe(
          map((checkResult) =>
            EmployeeActions.checkEmployeeCurrentSuccess({ checkResult })
          ),
          catchError((error) =>
            of(EmployeeActions.checkEmployeeCurrentFailure({ error }))
          )
        )
      )
    )
  );
}
