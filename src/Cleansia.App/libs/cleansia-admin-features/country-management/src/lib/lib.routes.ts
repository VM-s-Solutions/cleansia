import { Route } from '@angular/router';
import { CountryFormComponent } from './country-form/country-form.component';
import { CountryManagementComponent } from './country-management/country-management.component';

export const countryManagementRoutes: Route[] = [
  { path: '', component: CountryManagementComponent },
  {
    path: 'create',
    component: CountryFormComponent,
    data: { mode: 'create' },
  },
  {
    path: ':countryId/edit',
    component: CountryFormComponent,
    data: { mode: 'edit' },
  },
];