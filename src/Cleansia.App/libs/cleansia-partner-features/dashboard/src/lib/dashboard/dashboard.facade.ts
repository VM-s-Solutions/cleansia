import { Injectable, inject } from '@angular/core';
import { Router } from '@angular/router';
import { UnsubscribeControlDirective } from '@cleansia/directives';
import { OrderFilter } from '@cleansia/models';
import {
  OrderStatus,
  PartnerClient,
  SortDefinition,
  SortDirection,
} from '@cleansia/partner-services';
import * as DashboardActions from '@cleansia/partner-stores';
import {
  selectAnalyticsLoading,
  selectDashboardStats,
  selectDashboardStatsLoading,
  selectEarningsAnalytics,
  selectEarningsAnalyticsLoading,
  selectOrderAnalytics,
  selectOrderAnalyticsLoading,
  selectProductivityMetrics,
  selectProductivityMetricsLoading,
  selectSelectedDateRange,
  selectTimeAnalytics,
  selectTimeAnalyticsLoading,
  selectUpcomingOrders,
  selectUpcomingOrdersLoading,
} from '@cleansia/partner-stores';
import { Store } from '@ngrx/store';
import { TranslateService } from '@ngx-translate/core';
import { takeUntil } from 'rxjs';
import { StatCard } from './dashboard.models';

@Injectable()
export class DashboardFacade extends UnsubscribeControlDirective {
  private readonly store = inject(Store);
  private readonly router = inject(Router);
  private readonly partnerClient = inject(PartnerClient);
  private readonly translate = inject(TranslateService);
  private currentEmployeeId: string | null = null;

  readonly stats = this.store.selectSignal(selectDashboardStats);
  readonly upcomingOrders = this.store.selectSignal(selectUpcomingOrders);
  readonly statsLoading = this.store.selectSignal(selectDashboardStatsLoading);
  readonly upcomingOrdersLoading = this.store.selectSignal(
    selectUpcomingOrdersLoading
  );

  // Analytics signals
  readonly earningsAnalytics = this.store.selectSignal(selectEarningsAnalytics);
  readonly timeAnalytics = this.store.selectSignal(selectTimeAnalytics);
  readonly orderAnalytics = this.store.selectSignal(selectOrderAnalytics);
  readonly productivityMetrics = this.store.selectSignal(
    selectProductivityMetrics
  );
  readonly selectedDateRange = this.store.selectSignal(selectSelectedDateRange);

  // Loading signals
  readonly earningsLoading = this.store.selectSignal(
    selectEarningsAnalyticsLoading
  );
  readonly timeLoading = this.store.selectSignal(selectTimeAnalyticsLoading);
  readonly orderLoading = this.store.selectSignal(selectOrderAnalyticsLoading);
  readonly productivityLoading = this.store.selectSignal(
    selectProductivityMetricsLoading
  );
  readonly analyticsLoading = this.store.selectSignal(selectAnalyticsLoading);

  constructor() {
    super();
    this.loadDashboard();
  }

  private loadDashboard(): void {
    this.partnerClient.employeeClient
      .getCurrentEmployee()
      .pipe(takeUntil(this.destroyed$))
      .subscribe((employee) => {
        if (employee?.id) {
          this.currentEmployeeId = employee.id;
          this.loadDashboardData(employee.id);
          this.loadAnalytics(employee.id);
        }
      });
  }

  private loadDashboardData(employeeId: string): void {
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
        value: `${stats.currentPeriodEarnings.toLocaleString(
          this.translate.currentLang || 'cs-CZ'
        )} Kč`,
        icon: 'pi pi-wallet',
        color: '#8b5cf6',
        route: '/invoices',
      },
    ];
  }

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

  private loadAnalytics(employeeId: string): void {
    this.store.dispatch(DashboardActions.refreshAllAnalytics({ employeeId }));
  }

  onDateRangeChanged(startDate: Date, endDate: Date): void {
    this.store.dispatch(DashboardActions.setDateRange({ startDate, endDate }));

    if (this.currentEmployeeId) {
      this.store.dispatch(
        DashboardActions.refreshAllAnalytics({
          employeeId: this.currentEmployeeId,
        })
      );
    }
  }

  refreshAnalytics(): void {
    if (this.currentEmployeeId) {
      this.store.dispatch(
        DashboardActions.refreshAllAnalytics({
          employeeId: this.currentEmployeeId,
        })
      );
    }
  }

  navigateTo(route: string): void {
    this.router.navigate([route]);
  }

  refresh(): void {
    this.loadDashboard();
  }
}
