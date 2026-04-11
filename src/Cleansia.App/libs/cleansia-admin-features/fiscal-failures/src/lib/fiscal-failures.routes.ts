import { Route } from '@angular/router';

export const fiscalFailuresRoutes: Route[] = [
  {
    path: '',
    loadComponent: () =>
      import('./fiscal-failures-list/fiscal-failures-list.component').then(
        (m) => m.FiscalFailuresListComponent
      ),
    data: { title: 'page_titles.admin.fiscal_failures' },
  },
];
