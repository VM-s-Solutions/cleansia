import { inject, Injectable } from '@angular/core';
import { Actions, createEffect, ofType } from '@ngrx/effects';
import { CustomerClient } from '@cleansia/customer-services';
import { catchError, map, of, switchMap } from 'rxjs';
import * as OrderActions from './order.actions';

@Injectable()
export class CustomerOrderEffects {
  private readonly actions$ = inject(Actions);
  private readonly customerClient = inject(CustomerClient);

  loadOrders$ = createEffect(() =>
    this.actions$.pipe(
      ofType(OrderActions.loadCustomerOrders),
      switchMap((action) =>
        this.customerClient.orderClient
          .getPaged(
            undefined, // id
            undefined, // isActive
            undefined, // customerName
            undefined, // customerEmail
            undefined, // customerPhone
            undefined, // displayOrderNumber
            undefined, // employeeId
            action.cleaningDateFrom,
            action.cleaningDateTo,
            action.paymentStatuses,
            action.paymentTypes,
            undefined, // minTotalPrice
            undefined, // maxTotalPrice
            action.orderStatuses,
            undefined, // hasAvailableSpots
            undefined, // isUnassigned
            undefined, // excludeEmployeeId
            action.sort,
            action.offset,
            action.limit
          )
          .pipe(
            map((result) =>
              OrderActions.loadCustomerOrdersSuccess({
                data: result.data ?? [],
                total: result.total ?? 0,
              })
            ),
            catchError((error) =>
              of(OrderActions.loadCustomerOrdersFailure({ error }))
            )
          )
      )
    )
  );

  loadDetail$ = createEffect(() =>
    this.actions$.pipe(
      ofType(OrderActions.loadCustomerOrderDetail),
      switchMap((action) =>
        this.customerClient.orderClient.getById(action.orderId).pipe(
          map((order) => OrderActions.loadCustomerOrderDetailSuccess({ order })),
          catchError((error) =>
            of(OrderActions.loadCustomerOrderDetailFailure({ error }))
          )
        )
      )
    )
  );
}
