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
];
