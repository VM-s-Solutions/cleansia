import { Route } from '@angular/router';
import { UserLoyaltyDetailComponent } from './user-loyalty-detail/user-loyalty-detail.component';

export const loyaltyUserRoutes: Route[] = [
  {
    path: ':userId',
    component: UserLoyaltyDetailComponent,
    data: { title: 'page_titles.admin.loyalty_user_detail' },
  },
];
