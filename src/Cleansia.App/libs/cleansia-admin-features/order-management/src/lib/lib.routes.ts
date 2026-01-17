import { Route } from '@angular/router';
import { OrderManagementComponent } from './order-management/order-management.component';
import { OrderDetailComponent } from './order-detail/order-detail.component';

export const orderManagementRoutes: Route[] = [
  {
    path: '',
    component: OrderManagementComponent,
    data: { title: 'page_titles.admin.orders' },
  },
  {
    path: ':orderId',
    component: OrderDetailComponent,
    data: { title: 'page_titles.admin.order_details' },
  },
];
