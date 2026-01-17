import { Route } from '@angular/router';
import { PackageManagementComponent } from './package-management/package-management.component';
import { PackageFormComponent } from './package-form/package-form.component';

export const packageManagementRoutes: Route[] = [
  {
    path: '',
    component: PackageManagementComponent,
    data: { title: 'page_titles.admin.packages' },
  },
  {
    path: 'create',
    component: PackageFormComponent,
    data: { mode: 'create', title: 'page_titles.admin.package_create' },
  },
  {
    path: ':packageId/edit',
    component: PackageFormComponent,
    data: { mode: 'edit', title: 'page_titles.admin.package_edit' },
  },
];