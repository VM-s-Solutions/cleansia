import { Route } from '@angular/router';
import { PlusPageComponent } from './plus/plus-page.component';

export const plusRoutes: Route[] = [
  {
    path: '',
    component: PlusPageComponent,
    data: { title: 'page_titles.customer.plus' },
  },
];
