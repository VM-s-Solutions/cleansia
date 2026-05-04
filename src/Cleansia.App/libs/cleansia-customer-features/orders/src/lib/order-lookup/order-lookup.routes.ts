import { Route } from '@angular/router';
import { GuestOrderDetailComponent } from './guest-order-detail.component';
import { OrderLookupComponent } from './order-lookup.component';

/**
 * Public guest lookup routes — must be registered ABOVE the auth-guarded
 * `orders` route in app.routes.ts so these literal paths win the match.
 */
export const orderLookupRoutes: Route[] = [
  {
    path: '',
    component: OrderLookupComponent,
    data: { title: 'page_titles.customer.order_lookup' },
  },
  {
    path: ':orderId',
    component: GuestOrderDetailComponent,
    data: { title: 'page_titles.customer.order_lookup_detail' },
  },
];
