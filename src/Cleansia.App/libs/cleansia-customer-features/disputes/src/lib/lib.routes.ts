import { Route } from '@angular/router';
import { DisputesComponent } from './disputes/disputes.component';

export const disputesRoutes: Route[] = [
  {
    path: '',
    component: DisputesComponent,
    data: { title: 'page_titles.customer.disputes' },
  },
];
