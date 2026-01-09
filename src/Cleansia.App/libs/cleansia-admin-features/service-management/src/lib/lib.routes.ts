import { Route } from '@angular/router';
import { ServiceManagementComponent } from './service-management/service-management.component';
import { ServiceFormComponent } from './service-form/service-form.component';

export const serviceManagementRoutes: Route[] = [
  { path: '', component: ServiceManagementComponent },
  {
    path: 'create',
    component: ServiceFormComponent,
    data: { mode: 'create' },
  },
  {
    path: ':serviceId/edit',
    component: ServiceFormComponent,
    data: { mode: 'edit' },
  },
];