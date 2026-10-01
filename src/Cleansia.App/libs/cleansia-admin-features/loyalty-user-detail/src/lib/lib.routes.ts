import { Route } from '@angular/router';
import { CustomersListComponent } from './customers-list/customers-list.component';
import { UserLoyaltyDetailComponent } from './user-loyalty-detail/user-loyalty-detail.component';

/** The customer list -> /customers, and the per-customer screen: loyalty, referrals and credit -> /customers/:userId */
export const customerDetailRoutes: Route[] = [
  {
    path: '',
    component: CustomersListComponent,
    data: { title: 'page_titles.admin.customers' },
  },
  {
    path: ':userId',
    component: UserLoyaltyDetailComponent,
    data: { title: 'page_titles.admin.customer_detail' },
  },
];
