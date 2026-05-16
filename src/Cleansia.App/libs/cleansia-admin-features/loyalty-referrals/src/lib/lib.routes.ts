import { Route } from '@angular/router';
import { ReferralsListComponent } from './referrals-list/referrals-list.component';

export const loyaltyReferralsRoutes: Route[] = [
  {
    path: '',
    component: ReferralsListComponent,
    data: { title: 'page_titles.admin.loyalty_referrals' },
  },
];
