import { Route } from '@angular/router';
import { HomeComponent } from './home/home.component';

export const homeRoutes: Route[] = [
  {
    path: '',
    component: HomeComponent,
    data: { title: 'page_titles.customer.home' },
  },
];
