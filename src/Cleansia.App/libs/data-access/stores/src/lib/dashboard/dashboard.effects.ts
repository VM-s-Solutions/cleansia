import { inject, Injectable } from '@angular/core';
import { Client } from '@cleansia/services';
import { Actions, createEffect, ofType } from '@ngrx/effects';
import { catchError, forkJoin, map, mergeMap, of } from 'rxjs';
import * as DashboardActions from './dashboard.actions';

/**
 * NgRx Effects for dashboard data operations.
 * Simplified to use dedicated dashboard endpoints instead of multiple getPaged calls.
 */
@Injectable()
export class DashboardEffects {
  private readonly client = inject(Client);
  private readonly actions$ = inject(Actions);

  /**
   * Loads dashboard statistics using the dedicated endpoint.
   * Single API call replaces 5 previous calls.
   */
  loadDashboardStats$ = createEffect(() =>
    this.actions$.pipe(
      ofType(DashboardActions.loadDashboardStats),
      mergeMap(({ employeeId }) =>
        this.client.dashboardClient.getStats(employeeId).pipe(
          map((stats) => DashboardActions.loadDashboardStatsSuccess({ stats })),
          catchError((error) => of(DashboardActions.loadDashboardStatsFailure({ error })))
        )
      )
    )
  );

  /**
   * Loads upcoming orders for the dashboard.
   */
  loadUpcomingOrders$ = createEffect(() =>
    this.actions$.pipe(
      ofType(DashboardActions.loadUpcomingOrders),
      mergeMap(({ filter, sort, offset = 0, limit = 5 }) =>
        this.client.orderClient.getPaged(
          filter?.id,
          filter?.isActive,
          filter?.customerName,
          filter?.customerEmail,
          filter?.customerPhone,
          filter?.displayOrderNumber,
          filter?.employeeId,
          filter?.cleaningDateFrom,
          filter?.cleaningDateTo,
          filter?.paymentStatuses,
          filter?.paymentTypes,
          filter?.minTotalPrice,
          filter?.maxTotalPrice,
          filter?.orderStatuses,
          filter?.hasAvailableSpots,
          filter?.isUnassigned,
          filter?.excludeEmployeeId,
          sort,
          offset,
          limit
        ).pipe(
          map((result) =>
            DashboardActions.loadUpcomingOrdersSuccess({
              orders: result.data || [],
            })
          ),
          catchError((error) => of(DashboardActions.loadUpcomingOrdersFailure({ error })))
        )
      )
    )
  );
}
