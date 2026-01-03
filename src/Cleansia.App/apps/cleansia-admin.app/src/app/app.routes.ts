import { Route } from '@angular/router';
import { adminGuard } from '@cleansia/admin-services';

export const appRoutes: Route[] = [
  {
    path: '',
    redirectTo: 'employee-management',
    pathMatch: 'full',
  },
  {
    path: 'login',
    loadChildren: () =>
      import('@cleansia/admin-features/admin-login').then(
        (m) => m.adminLoginRoutes
      ),
  },
  {
    path: 'employee-management',
    canActivate: [adminGuard],
    loadChildren: () =>
      import('@cleansia/admin-features/employee-management').then(
        (m) => m.employeeManagementRoutes
      ),
  },
  {
    path: 'pay-periods',
    canActivate: [adminGuard],
    loadChildren: () =>
      import('@cleansia.app/pay-periods').then((m) => m.payPeriodsRoutes),
  },
  {
    path: 'order-management',
    canActivate: [adminGuard],
    loadChildren: () =>
      import('@cleansia/admin-features/order-management').then(
        (m) => m.orderManagementRoutes
      ),
  },
  {
    path: 'invoice-management',
    canActivate: [adminGuard],
    loadChildren: () =>
      import('@cleansia/admin-features/invoice-management').then(
        (m) => m.invoiceManagementRoutes
      ),
  },
  {
    path: 'reports',
    canActivate: [adminGuard],
    loadChildren: () =>
      import('@cleansia/admin-features/reports').then((m) => m.reportsRoutes),
  },
  {
    path: 'service-management',
    canActivate: [adminGuard],
    loadChildren: () =>
      import('@cleansia/admin-features/service-management').then(
        (m) => m.serviceManagementRoutes
      ),
  },
  {
    path: 'unauthorized',
    loadComponent: () =>
      import('./unauthorized/unauthorized.component').then(
        (m) => m.UnauthorizedComponent
      ),
  },
];
