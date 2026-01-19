import { Route } from '@angular/router';
import { OrderDetailsComponent } from './order-details';
import { OrdersComponent } from './orders';

export const ordersRoutes: Route[] = [
  {
    path: '',
    component: OrdersComponent,
    data: { title: 'page_titles.partner.orders' },
  },
  {
    path: ':orderId',
    component: OrderDetailsComponent,
    data: { title: 'page_titles.partner.order_details' },
  },
];
