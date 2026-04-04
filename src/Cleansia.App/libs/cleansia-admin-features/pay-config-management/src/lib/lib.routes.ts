import { Route } from '@angular/router';
import { PayConfigManagementComponent } from './pay-config-management/pay-config-management.component';
import { PayConfigFormComponent } from './pay-config-form/pay-config-form.component';

export const payConfigManagementRoutes: Route[] = [
  {
    path: '',
    component: PayConfigManagementComponent,
    data: { title: 'page_titles.admin.pay_configs' },
  },
  {
    path: 'create',
    component: PayConfigFormComponent,
    data: { mode: 'create', title: 'page_titles.admin.pay_config_create' },
  },
  {
    path: ':payConfigId/edit',
    component: PayConfigFormComponent,
    data: { mode: 'edit', title: 'page_titles.admin.pay_config_edit' },
  },
];
