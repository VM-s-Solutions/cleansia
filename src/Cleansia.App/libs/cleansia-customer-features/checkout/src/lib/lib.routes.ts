import { Route } from '@angular/router';
import { CheckoutSuccessComponent } from './checkout-success/checkout-success.component';
import { CheckoutCancelComponent } from './checkout-cancel/checkout-cancel.component';

export const checkoutRoutes: Route[] = [
  {
    path: 'success',
    component: CheckoutSuccessComponent,
    data: { title: 'page_titles.customer.checkout_success' },
  },
  {
    path: 'cancel',
    component: CheckoutCancelComponent,
    data: { title: 'page_titles.customer.checkout_cancel' },
  },
];
