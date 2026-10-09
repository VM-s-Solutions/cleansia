import { Route } from '@angular/router';
import { provideCharts, withDefaultRegisterables } from 'ng2-charts';

export const dashboardRoutes: Route[] = [
  {
    path: '',
    providers: [provideCharts(withDefaultRegisterables())],
    loadComponent: () =>
      import('./dashboard/dashboard.component').then((m) => m.DashboardComponent),
    data: { title: 'page_titles.partner.dashboard' },
  },
];
