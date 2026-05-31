import { Route } from '@angular/router';
import { CountryFormComponent } from './country-form/country-form.component';
import { CountryManagementComponent } from './country-management/country-management.component';
import { ServiceAreaManagementComponent } from './service-area-management/service-area-management.component';

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

/**
 * Exposed separately so the host app mounts it at its own top-level route
 * (`/service-area-management`) rather than as a nested page under country
 * management — it's a distinct concern (countries=catalog,
 * service-area=where-we-operate).
 */
export const serviceAreaManagementRoutes: Route[] = [
  {
    path: '',
    component: ServiceAreaManagementComponent,
    data: { title: 'page_titles.admin.service_area' },
  },
];