import { Route } from '@angular/router';
import { permissionGuard } from '@cleansia/admin-services';
import { Policy } from '@cleansia/services';
import { OrderManagementComponent } from './order-management/order-management.component';
import { OrderDetailComponent } from './order-detail/order-detail.component';
import { ReceivablesComponent } from './receivables/receivables.component';

export const orderManagementRoutes: Route[] = [
  {
    path: '',
    component: OrderManagementComponent,
    data: { title: 'page_titles.admin.orders' },
  },
  {
    path: 'receivables',
    component: ReceivablesComponent,
    canActivate: [permissionGuard],
    data: { title: 'page_titles.admin.receivables', permission: Policy.CanViewReceivables },
  },
  {
    path: ':orderId',
    component: OrderDetailComponent,
    data: { title: 'page_titles.admin.order_details' },
  },
];
