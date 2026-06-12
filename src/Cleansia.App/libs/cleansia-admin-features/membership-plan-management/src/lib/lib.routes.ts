import { Route } from '@angular/router';
import { MembershipPlanFormComponent } from './membership-plan-form/membership-plan-form.component';
import { MembershipPlanListComponent } from './membership-plan-list/membership-plan-list.component';

export const membershipPlanManagementRoutes: Route[] = [
  {
    path: '',
    component: MembershipPlanListComponent,
    data: { title: 'page_titles.admin.membership_plans' },
  },
  {
    path: 'new',
    component: MembershipPlanFormComponent,
    data: { mode: 'create', title: 'page_titles.admin.membership_plan_create' },
  },
  {
    path: ':id/edit',
    component: MembershipPlanFormComponent,
    data: { mode: 'edit', title: 'page_titles.admin.membership_plan_edit' },
  },
];
