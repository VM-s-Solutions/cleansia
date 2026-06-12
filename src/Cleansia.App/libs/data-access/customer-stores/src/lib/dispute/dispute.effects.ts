import { inject, Injectable } from '@angular/core';
import { Actions, createEffect, ofType } from '@ngrx/effects';
import { CustomerClient } from '@cleansia/customer-services';
import { catchError, map, of, switchMap } from 'rxjs';
import * as DisputeActions from './dispute.actions';

@Injectable()
export class CustomerDisputeEffects {
  private readonly actions$ = inject(Actions);
  private readonly customerClient = inject(CustomerClient);

  loadDisputes$ = createEffect(() =>
    this.actions$.pipe(
      ofType(DisputeActions.loadCustomerDisputes),
      switchMap((action) =>
        this.customerClient.disputeClient
          .getPaged(
            undefined, undefined, undefined, undefined,
            action.statuses, undefined, undefined, undefined,
            undefined, undefined, undefined, undefined,
            undefined, action.offset, action.limit
          )
          .pipe(
            map((result) =>
              DisputeActions.loadCustomerDisputesSuccess({
                data: result.data ?? [],
                total: result.total ?? 0,
              })
            ),
            catchError((error) =>
              of(DisputeActions.loadCustomerDisputesFailure({ error }))
            )
          )
      )
    )
  );

  loadDetail$ = createEffect(() =>
    this.actions$.pipe(
      ofType(DisputeActions.loadCustomerDisputeDetail),
      switchMap((action) =>
        this.customerClient.disputeClient.getById(action.disputeId).pipe(
          map((dispute) =>
            DisputeActions.loadCustomerDisputeDetailSuccess({ dispute })
          ),
          catchError((error) =>
            of(DisputeActions.loadCustomerDisputeDetailFailure({ error }))
          )
        )
      )
    )
  );
}
