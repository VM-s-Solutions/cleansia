import { Route } from '@angular/router';
import { TermsComponent } from './terms/terms.component';
import { PrivacyComponent } from './privacy/privacy.component';

export const termsRoutes: Route[] = [
  {
    path: '',
    component: TermsComponent,
    data: { title: 'terms_page.title' },
  },
];

export const privacyRoutes: Route[] = [
  {
    path: '',
    component: PrivacyComponent,
    data: { title: 'privacy_page.title' },
  },
];
