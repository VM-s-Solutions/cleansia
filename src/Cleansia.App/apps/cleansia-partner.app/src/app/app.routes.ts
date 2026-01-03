import { Route } from '@angular/router';
import { authGuard } from '@cleansia/partner-services';
import { CleansiaPartnerRoute } from '@cleansia/services';

export const appRoutes: Route[] = [
  {
    path: CleansiaPartnerRoute.LOGIN,
    loadChildren: () =>
      import('@cleansia-partner/login').then((m) => m.loginRoutes),
  },
  {
    path: CleansiaPartnerRoute.REGISTER,
    loadChildren: () =>
      import('@cleansia-partner/register').then((m) => m.registerRoutes),
  },
  {
    path: CleansiaPartnerRoute.CONFIRM_EMAIL,
    loadChildren: () =>
      import('@cleansia-partner/confirm-email').then(
        (m) => m.confirmEmailRoutes
      ),
  },
  {
    path: CleansiaPartnerRoute.FORGOT_PASSWORD,
    loadChildren: () =>
      import('@cleansia-partner/forgot-password').then(
        (m) => m.forgotPasswordRoutes
      ),
  },
  {
    path: CleansiaPartnerRoute.DASHBOARD,
    loadChildren: () =>
      import('@cleansia-partner/dashboard').then((m) => m.dashboardRoutes),
    canActivate: [authGuard],
  },
  {
    path: CleansiaPartnerRoute.PROFILE,
    loadChildren: () =>
      import('@cleansia-partner/profile').then((m) => m.profileRoutes),
    canActivate: [authGuard],
  },
  {
    path: CleansiaPartnerRoute.ORDERS,
    loadChildren: () =>
      import('@cleansia-partner/orders').then((m) => m.ordersRoutes),
    canActivate: [authGuard],
  },
  {
    path: CleansiaPartnerRoute.INVOICES,
    loadChildren: () =>
      import('@cleansia-partner/invoices').then((m) => m.invoicesRoutes),
    canActivate: [authGuard],
  },
  {
    path: CleansiaPartnerRoute.HOME,
    redirectTo: CleansiaPartnerRoute.ORDERS,
    pathMatch: 'full',
  },
  // {
  //   path: '**',
  //   redirectTo: CleansiaPartnerRoute.LOGIN,
  // },
];
