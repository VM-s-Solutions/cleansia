import { Route } from '@angular/router';
import { DisputeDetailComponent } from './dispute-detail/dispute-detail.component';
import { DisputesManagementComponent } from './disputes-management/disputes-management.component';

export const disputesManagementRoutes: Route[] = [
  {
    path: '',
    component: DisputesManagementComponent,
    data: { title: 'page_titles.admin.disputes' },
  },
  {
    path: ':disputeId',
    component: DisputeDetailComponent,
    data: { title: 'page_titles.admin.dispute_details' },
  },
];
