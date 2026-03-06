import { Route } from '@angular/router';
import { OrdersComponent } from './orders/orders.component';
import { OrderDetailComponent } from './order-detail/order-detail.component';

export const ordersRoutes: Route[] = [
  {
    path: '',
    component: OrdersComponent,
    data: { title: 'page_titles.customer.orders' },
  },
  {
    path: ':orderId',
    component: OrderDetailComponent,
    data: { title: 'page_titles.customer.order_detail' },
  },
];
