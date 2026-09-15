import { Route } from '@angular/router';
import { CompanySettingsComponent } from './company-settings/company-settings.component';

export const companySettingsRoutes: Route[] = [
  {
    path: '',
    component: CompanySettingsComponent,
    data: { title: 'page_titles.admin.company_settings' },
  },
];
