import { Route } from '@angular/router';
import { TermsComponent } from './terms/terms.component';
import { PrivacyComponent } from './privacy/privacy.component';
import { ComplaintsComponent } from './complaints/complaints.component';

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

export const complaintsRoutes: Route[] = [
  {
    path: '',
    component: ComplaintsComponent,
    data: { title: 'complaints_page.title' },
  },
];
