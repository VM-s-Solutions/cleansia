import { Route } from '@angular/router';
import { ServicesCatalogComponent } from './services-catalog/services-catalog.component';

export const servicesCatalogRoutes: Route[] = [
  {
    path: '',
    component: ServicesCatalogComponent,
    data: { title: 'page_titles.customer.services' },
  },
];
