import { Route } from '@angular/router';
import { ProfileComponent } from './profile/profile.component';
import { MembershipManagementComponent } from './membership/membership-management.component';
import { MembershipSubscribeComponent } from './membership/membership-subscribe.component';
import { MembershipWelcomeComponent } from './membership/membership-welcome.component';

export const profileRoutes: Route[] = [
  {
    path: '',
    component: ProfileComponent,
    data: { title: 'page_titles.customer.profile' },
  },
];

/**
 * Cleansia Plus management routes — exported separately so the customer
 * app can mount them at `/membership` while keeping the profile lib's
 * existing `/profile` routes intact.
 *
 * Recurring booking management is mounted as a sibling under `recurring/*`
 * so the URL hierarchy reads `/membership/recurring` and
 * `/membership/recurring/create`. The recurring lib owns its own internal
 * routes (list + create wizard).
 */
export const membershipRoutes: Route[] = [
  {
    path: '',
    component: MembershipManagementComponent,
    data: { title: 'page_titles.customer.membership' },
  },
  {
    path: 'subscribe',
    component: MembershipSubscribeComponent,
    data: { title: 'page_titles.customer.membership_subscribe' },
  },
  {
    // Post-purchase celebration. Stripe's Checkout success URL points here
    // (membership-subscribe builds it). Routed before the recurring child
    // route so /membership/welcome resolves before any greedy match.
    path: 'welcome',
    component: MembershipWelcomeComponent,
    data: { title: 'page_titles.customer.membership_welcome' },
  },
  {
    path: 'recurring',
    loadChildren: () =>
      import('@cleansia-customer/recurring-bookings').then(
        (m) => m.recurringBookingsRoutes,
      ),
  },
];
