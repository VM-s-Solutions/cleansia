import { Route } from '@angular/router';
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
    path: CleansiaPartnerRoute.PROFILE,
    loadChildren: () =>
      import('@cleansia-partner/profile').then((m) => m.profileRoutes),
  },
  {
    path: CleansiaPartnerRoute.ORDERS,
    loadChildren: () =>
      import('@cleansia-partner/orders').then((m) => m.ordersRoutes),
  },
  {
    path: CleansiaPartnerRoute.HOME,
    redirectTo: CleansiaPartnerRoute.DASHBOARD,
    pathMatch: 'full',
  },
  // {
  //   path: '**',
  //   redirectTo: CleansiaPartnerRoute.LOGIN,
  // },
];
