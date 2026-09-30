import { Route } from '@angular/router';
import { permissionGuard } from '@cleansia/admin-services';
import { Policy } from '@cleansia/services';

export const payPeriodsRoutes: Route[] = [
  {
    path: '',
    loadComponent: () =>
      import('./pay-period-management/pay-period-management.component').then(
        (m) => m.PayPeriodManagementComponent
      ),
    data: { title: 'page_titles.admin.pay_periods' },
  },
  {
    path: 'cash-held',
    loadComponent: () =>
      import('./cash-held/cash-held.component').then((m) => m.CashHeldComponent),
    canActivate: [permissionGuard],
    data: { title: 'page_titles.admin.cash_held', permission: Policy.CanViewCashHeld },
  },
  {
    path: ':id',
    loadComponent: () =>
      import('./pay-period-detail/pay-period-detail.component').then(
        (m) => m.PayPeriodDetailComponent
      ),
    data: { title: 'page_titles.admin.pay_period_details' },
  },
];
