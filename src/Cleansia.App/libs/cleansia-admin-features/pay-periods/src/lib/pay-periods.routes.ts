import { Route } from '@angular/router';

export const payPeriodsRoutes: Route[] = [
  {
    path: '',
    loadComponent: () =>
      import('./pay-period-management/pay-period-management.component').then(
        (m) => m.PayPeriodManagementComponent
      ),
  },
  {
    path: ':id',
    loadComponent: () =>
      import('./pay-period-detail/pay-period-detail.component').then(
        (m) => m.PayPeriodDetailComponent
      ),
  },
];
