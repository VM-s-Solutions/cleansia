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
  {
    // The public Cleansia Plus page. `MEMBERSHIP` below is the same product's
    // MANAGEMENT screen and stays guarded — this is the one an anonymous
    // visitor can read, and the one the nav bar's Plus link opens for them.
    path: CleansiaCustomerRoute.PLUS,
    loadChildren: () =>
      import('@cleansia-customer/plus').then((m) => m.plusRoutes),
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

  // Order wizard — PUBLIC, and deliberately so. `Order/CreateOrder` is
  // [AllowAnonymous] and the wizard branches on `isAuthenticated` throughout
  // (saved addresses, prefilled name, membership perks), so a guest can book
  // end to end. The comment here used to say "authenticated only", which is
  // the opposite of what the endpoint and the facade do; a guard added to
  // match it would have removed guest checkout.
  {
    path: CleansiaCustomerRoute.ORDER,
    loadChildren: () =>
      import('@cleansia-customer/order-wizard').then(
        (m) => m.orderWizardRoutes
      ),
  },

  // Guest order lookup used to live here as a SECOND form and a second result
  // screen. It is one page now — /track-order — so this redirects rather than
  // 404s: the path was in the footer and `/orders/lookup/:id` could be
  // bookmarked. Still above the auth-guarded `orders` route, so the literal
  // path keeps winning the match.
  //
  // Two routes and `pathMatch: 'full'`, not one prefix route: a PREFIX redirect
  // appends whatever it did not consume, so `/orders/lookup/<id>` became
  // `/track-order/<id>` — a path nothing serves — and landed on not-found.
  {
    path: CleansiaCustomerRoute.ORDERS + '/lookup',
    redirectTo: '/' + CleansiaCustomerRoute.TRACK_ORDER,
    pathMatch: 'full',
  },
  {
    path: CleansiaCustomerRoute.ORDERS + '/lookup/:orderId',
    redirectTo: '/' + CleansiaCustomerRoute.TRACK_ORDER,
    pathMatch: 'full',
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
    // `checkout` is a namespace for the two URLs Stripe returns to, not a page.
    // Without the redirect below its bare path matched and rendered an empty
    // outlet — a blank screen with a navbar — for anyone who trimmed the URL.
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
    // Imported statically. `@cleansia/components` is already in the main bundle
    // (app.ts, the navbar and the footer all use it), so deferring it here
    // saved nothing and made Nx treat the whole library as lazy-loaded — which
    // is what turned those three eager imports into lint errors.
    path: CleansiaCustomerRoute.NOT_FOUND,
    // The customer app's own 404: the shared component with this app's ways out
    // of it. The bare shared one still serves partner and admin.
    loadComponent: () =>
      import('./components/not-found/customer-not-found.component').then(
        (m) => m.CustomerNotFoundComponent,
      ),
    data: { title: 'page_titles.customer.not_found' },
  },
  {
    path: '**',
    redirectTo: CleansiaCustomerRoute.NOT_FOUND,
  },
];
