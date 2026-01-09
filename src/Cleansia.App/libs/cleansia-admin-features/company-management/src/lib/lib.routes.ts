import { Route } from '@angular/router';
import { CompanyInfoListComponent } from './company-info-list/company-info-list.component';
import { CompanyInfoFormComponent } from './company-info-form/company-info-form.component';

export const companyManagementRoutes: Route[] = [
  { path: '', component: CompanyInfoListComponent },
  { path: 'create', component: CompanyInfoFormComponent, data: { mode: 'create' } },
  { path: ':companyInfoId/edit', component: CompanyInfoFormComponent, data: { mode: 'edit' } },
];
