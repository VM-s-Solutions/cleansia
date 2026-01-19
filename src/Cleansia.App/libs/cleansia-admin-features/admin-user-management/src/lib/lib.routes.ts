import { Route } from '@angular/router';
import { AdminUserManagementComponent } from './admin-user-management/admin-user-management.component';
import { AdminUserFormComponent } from './admin-user-form/admin-user-form.component';

export const adminUserManagementRoutes: Route[] = [
  {
    path: '',
    component: AdminUserManagementComponent,
    data: { title: 'page_titles.admin.admin_users' },
  },
  {
    path: 'create',
    component: AdminUserFormComponent,
    data: { mode: 'create', title: 'page_titles.admin.admin_user_create' },
  },
  {
    path: ':userId/edit',
    component: AdminUserFormComponent,
    data: { mode: 'edit', title: 'page_titles.admin.admin_user_edit' },
  },
];