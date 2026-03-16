import { Route } from '@angular/router';
import { GdprComponent } from './gdpr/gdpr.component';

export const gdprRoutes: Route[] = [
  {
    path: '',
    component: GdprComponent,
    data: { title: 'page_titles.customer.gdpr' },
  },
];
