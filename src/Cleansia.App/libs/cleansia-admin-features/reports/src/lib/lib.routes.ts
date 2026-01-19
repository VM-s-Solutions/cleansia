import { Route } from '@angular/router';
import { ReportsComponent } from './reports/reports.component';

export const reportsRoutes: Route[] = [
  {
    path: '',
    component: ReportsComponent,
    data: { title: 'page_titles.admin.reports' },
  },
];