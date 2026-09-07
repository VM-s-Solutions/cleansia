import { Route } from '@angular/router';
import { CheckoutSuccessComponent } from './checkout-success/checkout-success.component';
import { CheckoutCancelComponent } from './checkout-cancel/checkout-cancel.component';

export const checkoutRoutes: Route[] = [
  {
    // Stripe returns to `checkout/success` and `checkout/cancel`; the bare
    // path is not a page. It used to match and render nothing, so trimming the
    // URL gave a blank screen rather than going anywhere.
    path: '',
    pathMatch: 'full',
    redirectTo: '/',
  },
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
