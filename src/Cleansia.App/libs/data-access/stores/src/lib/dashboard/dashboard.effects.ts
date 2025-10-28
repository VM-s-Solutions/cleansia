import { inject, Injectable } from '@angular/core';
import { Client } from '@cleansia/services';
import { Actions, createEffect, ofType } from '@ngrx/effects';
import { Store } from '@ngrx/store';
import { catchError, forkJoin, map, mergeMap, of, withLatestFrom } from 'rxjs';
import * as DashboardActions from './dashboard.actions';
import * as DashboardSelectors from './dashboard.selectors';

/**
 * NgRx Effects for dashboard data operations.
 * Simplified to use dedicated dashboard endpoints instead of multiple getPaged calls.
 */
@Injectable()
export class DashboardEffects {
  private readonly client = inject(Client);
  private readonly actions$ = inject(Actions);
  private readonly store = inject(Store);

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

  /**
   * Loads earnings analytics for the specified date range.
   */
  loadEarningsAnalytics$ = createEffect(() =>
    this.actions$.pipe(
      ofType(DashboardActions.loadEarningsAnalytics),
      mergeMap(({ employeeId, startDate, endDate }) =>
        this.client.dashboardClient.getEarningsAnalytics(employeeId, startDate, endDate).pipe(
          map((data) => DashboardActions.loadEarningsAnalyticsSuccess({ data })),
          catchError((error) => of(DashboardActions.loadEarningsAnalyticsFailure({ error })))
        )
      )
    )
  );

  /**
   * Loads time analytics for the specified date range.
   */
  loadTimeAnalytics$ = createEffect(() =>
    this.actions$.pipe(
      ofType(DashboardActions.loadTimeAnalytics),
      mergeMap(({ employeeId, startDate, endDate }) =>
        this.client.dashboardClient.getTimeAnalytics(employeeId, startDate, endDate).pipe(
          map((data) => DashboardActions.loadTimeAnalyticsSuccess({ data })),
          catchError((error) => of(DashboardActions.loadTimeAnalyticsFailure({ error })))
        )
      )
    )
  );

  /**
   * Loads order analytics for the specified date range.
   */
  loadOrderAnalytics$ = createEffect(() =>
    this.actions$.pipe(
      ofType(DashboardActions.loadOrderAnalytics),
      mergeMap(({ employeeId, startDate, endDate }) =>
        this.client.dashboardClient.getOrderAnalytics(employeeId, startDate, endDate).pipe(
          map((data) => DashboardActions.loadOrderAnalyticsSuccess({ data })),
          catchError((error) => of(DashboardActions.loadOrderAnalyticsFailure({ error })))
        )
      )
    )
  );

  /**
   * Loads productivity metrics for the employee.
   */
  loadProductivityMetrics$ = createEffect(() =>
    this.actions$.pipe(
      ofType(DashboardActions.loadProductivityMetrics),
      mergeMap(({ employeeId }) =>
        this.client.dashboardClient.getProductivityMetrics(employeeId).pipe(
          map((data) => DashboardActions.loadProductivityMetricsSuccess({ data })),
          catchError((error) => of(DashboardActions.loadProductivityMetricsFailure({ error })))
        )
      )
    )
  );

  /**
   * Refreshes all analytics when date range changes.
   */
  refreshAllAnalytics$ = createEffect(() =>
    this.actions$.pipe(
      ofType(DashboardActions.refreshAllAnalytics),
      withLatestFrom(this.store.select(DashboardSelectors.selectSelectedDateRange)),
      mergeMap(([{ employeeId }, dateRange]) => [
        DashboardActions.loadEarningsAnalytics({
          employeeId,
          startDate: dateRange.startDate,
          endDate: dateRange.endDate,
        }),
        DashboardActions.loadTimeAnalytics({
          employeeId,
          startDate: dateRange.startDate,
          endDate: dateRange.endDate,
        }),
        DashboardActions.loadOrderAnalytics({
          employeeId,
          startDate: dateRange.startDate,
          endDate: dateRange.endDate,
        }),
        DashboardActions.loadProductivityMetrics({ employeeId }),
      ])
    )
  );

  /**
   * Automatically refresh analytics when date range changes.
   */
  dateRangeChanged$ = createEffect(() =>
    this.actions$.pipe(
      ofType(DashboardActions.setDateRange),
      mergeMap(() => [
        // We would need employeeId here - this will be triggered from facade
        // For now, just return empty - facade will handle the refresh
      ])
    )
  , { dispatch: false });
}
