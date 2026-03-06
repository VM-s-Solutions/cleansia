import { Route } from '@angular/router';
import { OrderWizardComponent } from './order-wizard/order-wizard.component';

export const orderWizardRoutes: Route[] = [
  {
    path: '',
    component: OrderWizardComponent,
    data: { title: 'page_titles.customer.order' },
  },
];
