import { inject, Injectable } from '@angular/core';
import { Client, SnackbarService } from '@cleansia/services';
import { Actions, createEffect, ofType } from '@ngrx/effects';
import { Store } from '@ngrx/store';
import { TranslateService } from '@ngx-translate/core';
import { catchError, map, mergeMap, of } from 'rxjs';
import * as OrderActions from './order.actions';

@Injectable()
export class OrderEffects {
  private readonly client = inject(Client);
  private readonly actions$ = inject(Actions);
  private readonly store = inject(Store);
  private readonly translate = inject(TranslateService);
  private readonly snackbarService = inject(SnackbarService);

  loadPaged$ = createEffect(() =>
    this.actions$.pipe(
      ofType(OrderActions.loadOrderPaged),
      mergeMap((req) =>
        this.client.orderClient
          .getPaged(
            req.filter?.id,
            req.isActive,
            req.filter?.customerName,
            req.filter?.customerEmail,
            req.filter?.customerPhone,
            req.filter?.displayOrderNumber,
            req.filter?.employeeId,
            req.filter?.cleaningDateFrom,
            req.filter?.cleaningDateTo,
            req.filter?.paymentStatuses,
            req.filter?.paymentTypes,
            req.filter?.minTotalPrice,
            req.filter?.maxTotalPrice,
            req.filter?.orderStatuses,
            req.sort,
            req.offset,
            req.limit
          )
          .pipe(
            map((page) => OrderActions.loadOrderPagedSuccess({ page })),
            catchError((error) =>
              of(OrderActions.loadOrderPagedFailure({ error }))
            )
          )
      )
    )
  );

  loadDetail$ = createEffect(() =>
    this.actions$.pipe(
      ofType(OrderActions.loadOrderDetail),
      mergeMap(({ id }) =>
        this.client.orderClient.getPaged(id).pipe(
          map((page) => {
            const order = page.data?.[0];
            if (order) {
              return OrderActions.loadOrderDetailSuccess({ order });
            } else {
              return OrderActions.loadOrderDetailFailure({
                error: { message: 'Order not found' } as any,
              });
            }
          }),
          catchError((error) =>
            of(OrderActions.loadOrderDetailFailure({ error }))
          )
        )
      )
    )
  );
}
