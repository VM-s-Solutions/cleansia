import { Route } from '@angular/router';
import { OrderDetailsComponent } from './order-details';
import { OrdersComponent } from './orders';

export const ordersRoutes: Route[] = [
  { path: '', component: OrdersComponent },
  { path: ':orderId', component: OrderDetailsComponent },
];
