import { Route } from '@angular/router';
import { ExtraManagementComponent } from './extra-management/extra-management.component';
import { ExtraFormComponent } from './extra-form/extra-form.component';

export const extraManagementRoutes: Route[] = [
  {
    path: '',
    component: ExtraManagementComponent,
    data: { title: 'page_titles.admin.extras' },
  },
  {
    path: 'create',
    component: ExtraFormComponent,
    data: { mode: 'create', title: 'page_titles.admin.extra_create' },
  },
  {
    path: ':extraId/edit',
    component: ExtraFormComponent,
    data: { mode: 'edit', title: 'page_titles.admin.extra_edit' },
  },
];
