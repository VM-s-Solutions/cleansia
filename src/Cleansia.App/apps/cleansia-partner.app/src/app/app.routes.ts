import { Route } from '@angular/router';
import { authGuard, guestGuard } from '@cleansia/partner-services';
import { CleansiaPartnerRoute, CommonRoute } from '@cleansia/services';

export const appRoutes: Route[] = [
  {
    path: CleansiaPartnerRoute.LOGIN,
    loadChildren: () =>
      import('@cleansia-partner/login').then((m) => m.loginRoutes),
    canActivate: [guestGuard],
  },
  {
    path: CleansiaPartnerRoute.REGISTER,
    loadChildren: () =>
      import('@cleansia-partner/register').then((m) => m.registerRoutes),
    canActivate: [guestGuard],
  },
  {
    path: CleansiaPartnerRoute.CONFIRM_EMAIL,
    loadChildren: () =>
      import('@cleansia-partner/confirm-email').then(
        (m) => m.confirmEmailRoutes
      ),
    canActivate: [guestGuard],
  },
  {
    path: CleansiaPartnerRoute.FORGOT_PASSWORD,
    loadChildren: () =>
      import('@cleansia-partner/forgot-password').then(
        (m) => m.forgotPasswordRoutes
      ),
    canActivate: [guestGuard],
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
    path: CleansiaPartnerRoute.GDPR,
    loadChildren: () =>
      import('@cleansia-partner/gdpr').then((m) => m.gdprRoutes),
    canActivate: [authGuard],
  },
  {
    path: CleansiaPartnerRoute.HOME,
    redirectTo: CleansiaPartnerRoute.ORDERS,
    pathMatch: 'full',
  },
  {
    path: CommonRoute.NOT_FOUND,
    loadComponent: () =>
      import('@cleansia/components').then((m) => m.CleansiaNotFoundComponent),
    data: { title: 'page_titles.partner.not_found' },
  },
  {
    path: '**',
    redirectTo: CommonRoute.NOT_FOUND,
  },
];
