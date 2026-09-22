import { Route } from '@angular/router';
import { TermsComponent } from './terms/terms.component';
import { PrivacyComponent } from './privacy/privacy.component';
import { WorkContractComponent } from './work-contract/work-contract.component';

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

export const workContractRoutes: Route[] = [
  {
    path: '',
    component: WorkContractComponent,
    data: { title: 'work_contract_page.title' },
  },
];
