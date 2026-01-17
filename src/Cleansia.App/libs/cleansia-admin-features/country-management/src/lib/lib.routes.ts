import { Route } from '@angular/router';
import { CountryFormComponent } from './country-form/country-form.component';
import { CountryManagementComponent } from './country-management/country-management.component';

export const countryManagementRoutes: Route[] = [
  {
    path: '',
    component: CountryManagementComponent,
    data: { title: 'page_titles.admin.countries' },
  },
  {
    path: 'create',
    component: CountryFormComponent,
    data: { mode: 'create', title: 'page_titles.admin.country_create' },
  },
  {
    path: ':countryId/edit',
    component: CountryFormComponent,
    data: { mode: 'edit', title: 'page_titles.admin.country_edit' },
  },
];