import { Route } from '@angular/router';
import { adminGuard, guestGuard } from '@cleansia/admin-services';
import { CommonRoute } from '@cleansia/services';

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
    canActivate: [guestGuard],
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
    path: 'package-management',
    canActivate: [adminGuard],
    loadChildren: () =>
      import('@cleansia/admin-features/package-management').then(
        (m) => m.packageManagementRoutes
      ),
  },
  {
    path: 'admin-user-management',
    canActivate: [adminGuard],
    loadChildren: () =>
      import('@cleansia/admin-features/admin-user-management').then(
        (m) => m.adminUserManagementRoutes
      ),
  },
  {
    path: 'language-management',
    canActivate: [adminGuard],
    loadChildren: () =>
      import('@cleansia/admin-features/language-management').then(
        (m) => m.languageManagementRoutes
      ),
  },
  {
    path: 'country-management',
    canActivate: [adminGuard],
    loadChildren: () =>
      import('@cleansia/admin-features/country-management').then(
        (m) => m.countryManagementRoutes
      ),
  },
  {
    path: 'currency-management',
    canActivate: [adminGuard],
    loadChildren: () =>
      import('@cleansia/admin-features/currency-management').then(
        (m) => m.currencyManagementRoutes
      ),
  },
  {
    path: 'company-info',
    canActivate: [adminGuard],
    loadChildren: () =>
      import('@cleansia/admin-features/company-management').then(
        (m) => m.companyManagementRoutes
      ),
  },
  {
    path: 'pay-config-management',
    canActivate: [adminGuard],
    loadChildren: () =>
      import('@cleansia/admin-features/pay-config-management').then(
        (m) => m.payConfigManagementRoutes
      ),
  },
  {
    path: 'template-management',
    canActivate: [adminGuard],
    loadChildren: () =>
      import('@cleansia/admin-features/template-management').then(
        (m) => m.templateManagementRoutes
      ),
  },
  {
    path: 'fiscal-failures',
    canActivate: [adminGuard],
    loadChildren: () =>
      import('@cleansia/admin-features/fiscal-failures').then(
        (m) => m.fiscalFailuresRoutes
      ),
  },
  {
    path: 'loyalty/promos',
    canActivate: [adminGuard],
    loadChildren: () =>
      import('@cleansia/admin-features/loyalty-promo-codes').then(
        (m) => m.promoCodesRoutes
      ),
  },
  {
    path: 'loyalty/tiers',
    canActivate: [adminGuard],
    loadChildren: () =>
      import('@cleansia/admin-features/loyalty-tier-configs').then(
        (m) => m.loyaltyTiersRoutes
      ),
  },
  {
    path: 'loyalty/users',
    canActivate: [adminGuard],
    loadChildren: () =>
      import('@cleansia/admin-features/loyalty-user-detail').then(
        (m) => m.loyaltyUserRoutes
      ),
  },
  {
    path: 'loyalty/referrals',
    canActivate: [adminGuard],
    loadChildren: () =>
      import('@cleansia/admin-features/loyalty-referrals').then(
        (m) => m.loyaltyReferralsRoutes
      ),
  },
  {
    path: 'marketing',
    canActivate: [adminGuard],
    loadChildren: () =>
      import('@cleansia/admin-features/marketing').then(
        (m) => m.marketingRoutes
      ),
  },
  {
    path: 'unauthorized',
    loadComponent: () =>
      import('./unauthorized/unauthorized.component').then(
        (m) => m.UnauthorizedComponent
      ),
  },
  {
    path: CommonRoute.NOT_FOUND,
    loadComponent: () =>
      import('@cleansia/components').then((m) => m.CleansiaNotFoundComponent),
    data: { title: 'page_titles.admin.not_found' },
  },
  {
    path: '**',
    redirectTo: CommonRoute.NOT_FOUND,
  },
];
