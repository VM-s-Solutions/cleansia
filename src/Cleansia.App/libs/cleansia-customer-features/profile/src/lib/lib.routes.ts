import { Route } from '@angular/router';
import { CleansiaCustomerRoute } from '@cleansia/services';
import { ProfileComponent } from './profile/profile.component';
import { MembershipWelcomeComponent } from './membership/membership-welcome.component';
import { SavedAddressesComponent } from './saved-addresses/saved-addresses.component';

export const profileRoutes: Route[] = [
  {
    path: '',
    component: ProfileComponent,
    data: { title: 'page_titles.customer.profile' },
  },
];

export const savedAddressesRoutes: Route[] = [
  {
    path: '',
    component: SavedAddressesComponent,
    data: { title: 'page_titles.customer.saved_addresses' },
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
    // `/plus` is the product's ONE page now: the benefits for everyone, and
    // the management panel on top for a member. This URL kept working for
    // every bookmark, link and redirect that already pointed at it — including
    // Stripe's own cancel URL — so it redirects rather than 404s.
    //
    // `/membership/subscribe` went with it. It was a THIRD sales surface for
    // the same product, with its own plan picker and its own stylesheet, and
    // the plan cards on /plus already did the same job.
    path: '',
    pathMatch: 'full',
    redirectTo: '/' + CleansiaCustomerRoute.PLUS,
  },
  {
    path: 'subscribe',
    pathMatch: 'full',
    redirectTo: '/' + CleansiaCustomerRoute.PLUS,
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
