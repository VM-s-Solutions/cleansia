import { Route } from '@angular/router';
import { TierConfigsComponent } from './tier-configs/tier-configs.component';

export const loyaltyTiersRoutes: Route[] = [
  {
    path: '',
    component: TierConfigsComponent,
    data: { title: 'page_titles.admin.loyalty_tiers' },
  },
];
