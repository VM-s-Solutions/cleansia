import { Route } from '@angular/router';

export const dashboardRoutes: Route[] = [
  {
    path: '',
    loadComponent: () =>
      import('./dashboard/dashboard.component').then((m) => m.DashboardComponent),
    data: { title: 'page_titles.partner.dashboard' },
  },
];
