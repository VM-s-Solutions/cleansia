import { Route } from '@angular/router';
import { UserLoyaltyDetailComponent } from './user-loyalty-detail/user-loyalty-detail.component';

/** The per-customer detail screen: loyalty, referrals and credit. -> /customers/:userId */
export const customerDetailRoutes: Route[] = [
  {
    path: ':userId',
    component: UserLoyaltyDetailComponent,
    data: { title: 'page_titles.admin.customer_detail' },
  },
];
