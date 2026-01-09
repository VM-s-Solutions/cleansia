import { Route } from '@angular/router';
import { AdminUserManagementComponent } from './admin-user-management/admin-user-management.component';
import { AdminUserFormComponent } from './admin-user-form/admin-user-form.component';

export const adminUserManagementRoutes: Route[] = [
  { path: '', component: AdminUserManagementComponent },
  {
    path: 'create',
    component: AdminUserFormComponent,
    data: { mode: 'create' },
  },
  {
    path: ':userId/edit',
    component: AdminUserFormComponent,
    data: { mode: 'edit' },
  },
];