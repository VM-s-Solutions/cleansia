import { Route } from '@angular/router';
import { ServiceManagementComponent } from './service-management/service-management.component';
import { ServiceFormComponent } from './service-form/service-form.component';

export const serviceManagementRoutes: Route[] = [
  {
    path: '',
    component: ServiceManagementComponent,
    data: { title: 'page_titles.admin.services' },
  },
  {
    path: 'create',
    component: ServiceFormComponent,
    data: { mode: 'create', title: 'page_titles.admin.service_create' },
  },
  {
    path: ':serviceId/edit',
    component: ServiceFormComponent,
    data: { mode: 'edit', title: 'page_titles.admin.service_edit' },
  },
];