import { Route } from '@angular/router';
import { RewardsComponent } from './rewards/rewards.component';
import { RewardsActivityComponent } from './rewards/rewards-activity.component';

export const rewardsRoutes: Route[] = [
  {
    path: '',
    component: RewardsComponent,
    data: { title: 'page_titles.customer.rewards' },
  },
  {
    path: 'activity',
    component: RewardsActivityComponent,
    data: { title: 'page_titles.customer.rewards_activity' },
  },
];
