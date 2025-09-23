import { Route } from '@angular/router';
import { OrderDetailsComponent } from './order-details';
import { OrdersComponent } from './orders/orders.component';

export const ordersRoutes: Route[] = [
  { path: '', component: OrdersComponent },
  { path: ':orderId', component: OrderDetailsComponent },
];
