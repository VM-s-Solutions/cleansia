import { inject, Injectable } from '@angular/core';
import {
  Client,
  CompleteOrderCommand,
  OrderStatus,
  SnackbarService,
} from '@cleansia/services';
import { Actions, createEffect, ofType } from '@ngrx/effects';
import { Store } from '@ngrx/store';
import { TranslateService } from '@ngx-translate/core';
import { catchError, map, mergeMap, of, withLatestFrom } from 'rxjs';
import { CodeTypes } from '../code/code-types';
import { selectCodeByTypeAndValue } from '../code/code.selectors';
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
            req.filter?.hasAvailableSpots,
            req.filter?.isUnassigned,
            req.filter?.excludeEmployeeId,
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
        this.client.orderClient.getById(id).pipe(
          map((order) => OrderActions.loadOrderDetailSuccess({ order })),
          catchError((error) =>
            of(OrderActions.loadOrderDetailFailure({ error }))
          )
        )
      )
    )
  );

  completeOrder$ = createEffect(() =>
    this.actions$.pipe(
      ofType(OrderActions.completeOrder),
      mergeMap(
        ({
          orderId,
          employeeId,
          actualCompletionTimeMinutes,
          completionNotes,
        }) =>
          this.client.orderClient
            .completeOrder(
              new CompleteOrderCommand({
                orderId,
                employeeId,
                actualCompletionTimeMinutes,
                completionNotes,
              })
            )
            .pipe(
              withLatestFrom(
                this.store.select(
                  selectCodeByTypeAndValue(
                    CodeTypes.ORDER_STATUS,
                    OrderStatus.Completed
                  )
                )
              ),
              map(([response, completedStatusCode]) => {
                this.snackbarService.showSuccess(
                  this.translate.instant('pages.orders.complete_order.success')
                );
                return OrderActions.completeOrderSuccess({
                  orderId: response.orderId!,
                  orderStatus: completedStatusCode?.name || 'Completed',
                });
              }),
              catchError((error) => {
                this.snackbarService.showError(
                  this.translate.instant('pages.orders.complete_order.error')
                );
                return of(OrderActions.completeOrderFailure({ error }));
              })
            )
      )
    )
  );

  reloadOrdersAfterComplete$ = createEffect(() =>
    this.actions$.pipe(
      ofType(OrderActions.completeOrderSuccess),
      map(() => OrderActions.clearOrderState())
    )
  );
}
