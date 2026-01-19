import { Route } from '@angular/router';
import { CompanyInfoListComponent } from './company-info-list/company-info-list.component';
import { CompanyInfoFormComponent } from './company-info-form/company-info-form.component';

export const companyManagementRoutes: Route[] = [
  {
    path: '',
    component: CompanyInfoListComponent,
    data: { title: 'page_titles.admin.company_info' },
  },
  {
    path: 'create',
    component: CompanyInfoFormComponent,
    data: { mode: 'create', title: 'page_titles.admin.company_info_create' },
  },
  {
    path: ':companyInfoId/edit',
    component: CompanyInfoFormComponent,
    data: { mode: 'edit', title: 'page_titles.admin.company_info_edit' },
  },
];
