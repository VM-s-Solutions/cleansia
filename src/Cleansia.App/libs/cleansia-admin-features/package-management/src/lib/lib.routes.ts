import { Route } from '@angular/router';
import { PackageManagementComponent } from './package-management/package-management.component';
import { PackageFormComponent } from './package-form/package-form.component';

export const packageManagementRoutes: Route[] = [
  { path: '', component: PackageManagementComponent },
  {
    path: 'create',
    component: PackageFormComponent,
    data: { mode: 'create' },
  },
  {
    path: ':packageId/edit',
    component: PackageFormComponent,
    data: { mode: 'edit' },
  },
];