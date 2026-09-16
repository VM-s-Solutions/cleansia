import { Route } from '@angular/router';
import { CompanyLifecycleComponent } from './company-lifecycle/company-lifecycle.component';

export const companyLifecycleRoutes: Route[] = [
  {
    path: '',
    component: CompanyLifecycleComponent,
    data: { title: 'page_titles.admin.company_lifecycle' },
  },
];
