import { Route } from '@angular/router';
import {
  customerAuthGuard,
  customerGuestGuard,
} from '@cleansia/customer-services';
import { CleansiaCustomerRoute } from '@cleansia/services';

export const appRoutes: Route[] = [
  // Public routes
  {
    path: CleansiaCustomerRoute.HOME,
    loadChildren: () =>
      import('@cleansia-customer/home').then((m) => m.homeRoutes),
    pathMatch: 'full',
  },
  {
    path: CleansiaCustomerRoute.SERVICES,
    loadChildren: () =>
      import('@cleansia-customer/services-catalog').then(
        (m) => m.servicesCatalogRoutes
      ),
  },

  // Guest-only routes (redirect to orders if logged in)
  {
    path: CleansiaCustomerRoute.LOGIN,
    loadChildren: () =>
      import('@cleansia-customer/login').then((m) => m.loginRoutes),
    canActivate: [customerGuestGuard],
  },
  {
    path: CleansiaCustomerRoute.REGISTER,
    loadChildren: () =>
      import('@cleansia-customer/register').then((m) => m.registerRoutes),
    canActivate: [customerGuestGuard],
  },
  {
    // Referral landing — pre-fills the registration form with the shared
    // code; a logged-in invitee is bounced to orders by the guest guard.
    path: CleansiaCustomerRoute.REFERRAL_LANDING + '/:code',
    loadChildren: () =>
      import('@cleansia-customer/register').then(
        (m) => m.referralLandingRoutes
      ),
    canActivate: [customerGuestGuard],
  },
  {
    path: CleansiaCustomerRoute.CONFIRM_EMAIL,
    loadChildren: () =>
      import('@cleansia-customer/confirm-email').then(
        (m) => m.confirmEmailRoutes
      ),
  },
  {
    path: CleansiaCustomerRoute.FORGOT_PASSWORD,
    loadChildren: () =>
      import('@cleansia-customer/forgot-password').then(
        (m) => m.forgotPasswordRoutes
      ),
    canActivate: [customerGuestGuard],
  },

  // Track order (public — no auth required)
  {
    path: CleansiaCustomerRoute.TRACK_ORDER,
    loadComponent: () =>
      import('@cleansia-customer/orders').then(
        (m) => m.TrackOrderComponent
      ),
    data: { title: 'page_titles.customer.track_order' },
  },

  // Order wizard — authenticated only (per Customer App Requirements)
  {
    path: CleansiaCustomerRoute.ORDER,
    loadChildren: () =>
      import('@cleansia-customer/order-wizard').then(
        (m) => m.orderWizardRoutes
      ),
  },

  // Guest order lookup (public — no auth) — MUST come before the auth-guarded
  // `orders` route so the literal `orders/lookup` path wins the match.
  {
    path: CleansiaCustomerRoute.ORDERS + '/lookup',
    loadChildren: () =>
      import('@cleansia-customer/orders').then((m) => m.orderLookupRoutes),
  },

  // Protected routes (require login)
  {
    path: CleansiaCustomerRoute.ORDERS,
    loadChildren: () =>
      import('@cleansia-customer/orders').then((m) => m.ordersRoutes),
    canActivate: [customerAuthGuard],
  },
  {
    path: CleansiaCustomerRoute.PROFILE,
    loadChildren: () =>
      import('@cleansia-customer/profile').then((m) => m.profileRoutes),
    canActivate: [customerAuthGuard],
  },
  {
    path: CleansiaCustomerRoute.SAVED_ADDRESSES,
    loadChildren: () =>
      import('@cleansia-customer/profile').then((m) => m.savedAddressesRoutes),
    canActivate: [customerAuthGuard],
  },
  {
    path: CleansiaCustomerRoute.DISPUTES,
    loadChildren: () =>
      import('@cleansia-customer/disputes').then((m) => m.disputesRoutes),
    canActivate: [customerAuthGuard],
  },
  {
    path: CleansiaCustomerRoute.REWARDS,
    loadChildren: () =>
      import('@cleansia-customer/rewards').then((m) => m.rewardsRoutes),
    canActivate: [customerAuthGuard],
  },
  {
    path: CleansiaCustomerRoute.MEMBERSHIP,
    loadChildren: () =>
      import('@cleansia-customer/profile').then((m) => m.membershipRoutes),
    canActivate: [customerAuthGuard],
  },
  {
    path: 'checkout',
    loadChildren: () =>
      import('@cleansia-customer/checkout').then((m) => m.checkoutRoutes),
  },

  // Legal / policy pages (public)
  {
    path: CleansiaCustomerRoute.GDPR,
    loadChildren: () =>
      import('@cleansia-customer/gdpr').then((m) => m.gdprRoutes),
  },
  {
    path: 'terms',
    loadChildren: () =>
      import('@cleansia-customer/legal-pages').then((m) => m.termsRoutes),
  },
  {
    path: 'privacy',
    loadChildren: () =>
      import('@cleansia-customer/legal-pages').then((m) => m.privacyRoutes),
  },

  // 404
  {
    path: CleansiaCustomerRoute.NOT_FOUND,
    loadComponent: () =>
      import('@cleansia/components').then((m) => m.CleansiaNotFoundComponent),
    data: { title: 'page_titles.customer.not_found' },
  },
  {
    path: '**',
    redirectTo: CleansiaCustomerRoute.NOT_FOUND,
  },
];
