import { Injectable, inject } from '@angular/core';
import { Router } from '@angular/router';
import { UnsubscribeControlDirective } from '@cleansia/directives';
import { OrderFilter } from '@cleansia/models';
import {
  Client,
  OrderStatus,
  SortDefinition,
  SortDirection,
} from '@cleansia/services';
import * as DashboardActions from '@cleansia/stores';
import {
  selectDashboardStats,
  selectDashboardStatsLoading,
  selectUpcomingOrders,
  selectUpcomingOrdersLoading,
} from '@cleansia/stores';
import { Store } from '@ngrx/store';
import { catchError, of, takeUntil } from 'rxjs';
import { StatCard } from './dashboard.models';

/**
 * Dashboard facade service that manages dashboard state and business logic.
 * Simplified to work with dedicated dashboard endpoints.
 */
@Injectable()
export class DashboardFacade extends UnsubscribeControlDirective {
  private readonly store = inject(Store);
  private readonly router = inject(Router);
  private readonly client = inject(Client);

  readonly stats = this.store.selectSignal(selectDashboardStats);
  readonly upcomingOrders = this.store.selectSignal(selectUpcomingOrders);
  readonly statsLoading = this.store.selectSignal(selectDashboardStatsLoading);
  readonly upcomingOrdersLoading = this.store.selectSignal(
    selectUpcomingOrdersLoading
  );

  constructor() {
    super();
    this.loadDashboard();
  }

  /**
   * Loads dashboard data for the current employee.
   */
  private loadDashboard(): void {
    this.client.employeeClient
      .getCurrentEmployee()
      .pipe(
        takeUntil(this.destroyed$),
        catchError(() => of(null))
      )
      .subscribe((employee) => {
        if (employee?.id) {
          this.loadDashboardData(employee.id);
        }
      });
  }

  /**
   * Dispatches actions to load dashboard statistics and upcoming orders.
   */
  private loadDashboardData(employeeId: string): void {
    // Load stats using dedicated endpoint
    this.store.dispatch(DashboardActions.loadDashboardStats({ employeeId }));

    // Load upcoming orders
    const upcomingOrdersFilter = new OrderFilter({
      employeeId,
      cleaningDateFrom: new Date(),
      orderStatuses: [
        OrderStatus.Pending,
        OrderStatus.Confirmed,
        OrderStatus.InProgress,
      ],
    });

    this.store.dispatch(
      DashboardActions.loadUpcomingOrders({
        filter: upcomingOrdersFilter,
        sort: [
          new SortDefinition({
            field: 'CleaningDateTime',
            direction: SortDirection.Ascending,
          }),
        ],
        offset: 0,
        limit: 5,
      })
    );
  }

  /**
   * Gets stat cards for display.
   */
  getStatCards(): StatCard[] {
    const stats = this.stats();
    if (!stats) {
      return [];
    }

    const trend = this.calculateTrend(
      stats.thisMonthCompletedOrders,
      stats.lastMonthCompletedOrders
    );

    return [
      {
        title: 'pages.dashboard.available_orders',
        value: stats.availableOrdersCount,
        icon: 'pi pi-inbox',
        color: '#0284c7',
        route: '/orders',
      },
      {
        title: 'pages.dashboard.my_active_orders',
        value: stats.myActiveOrdersCount,
        icon: 'pi pi-clock',
        color: '#f59e0b',
        route: '/orders',
      },
      {
        title: 'pages.dashboard.completed_this_month',
        value: stats.thisMonthCompletedOrders,
        icon: 'pi pi-check-circle',
        color: '#10b981',
        trend,
      },
      {
        title: 'pages.dashboard.pending_earnings',
        value: `${stats.currentPeriodEarnings.toLocaleString('cs-CZ')} Kč`,
        icon: 'pi pi-wallet',
        color: '#8b5cf6',
        route: '/invoices',
      },
    ];
  }

  /**
   * Calculates trend percentage between current and previous values.
   */
  private calculateTrend(
    current: number,
    previous: number
  ): {
    value: number;
    direction: 'up' | 'down' | 'neutral';
  } {
    if (previous === 0) {
      return { value: 0, direction: 'neutral' };
    }

    const percentChange = ((current - previous) / previous) * 100;

    return {
      value: Math.abs(Math.round(percentChange)),
      direction:
        percentChange > 0 ? 'up' : percentChange < 0 ? 'down' : 'neutral',
    };
  }

  /**
   * Navigates to a route.
   */
  navigateTo(route: string): void {
    this.router.navigate([route]);
  }

  /**
   * Refreshes dashboard data.
   */
  refresh(): void {
    this.loadDashboard();
  }

  override ngOnDestroy(): void {
    this.store.dispatch(DashboardActions.clearDashboard());
    super.ngOnDestroy();
  }
}
