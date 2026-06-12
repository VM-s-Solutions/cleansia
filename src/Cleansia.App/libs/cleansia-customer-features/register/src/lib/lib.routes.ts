import { Route } from '@angular/router';
import { RegisterComponent } from './register/register.component';

export const registerRoutes: Route[] = [
  {
    path: '',
    component: RegisterComponent,
    data: { title: 'page_titles.customer.register' },
  },
];

// Mounted at /r/:code — the same registration experience with the shared
// referral code captured from the URL and pre-applied fail-soft.
export const referralLandingRoutes: Route[] = [
  {
    path: '',
    component: RegisterComponent,
    data: { title: 'page_titles.customer.register' },
  },
];
