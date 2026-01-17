import { Route } from '@angular/router';
import { CurrencyFormComponent } from './currency-form/currency-form.component';
import { CurrencyManagementComponent } from './currency-management/currency-management.component';

export const currencyManagementRoutes: Route[] = [
  {
    path: '',
    component: CurrencyManagementComponent,
    data: { title: 'page_titles.admin.currencies' },
  },
  {
    path: 'create',
    component: CurrencyFormComponent,
    data: { mode: 'create', title: 'page_titles.admin.currency_create' },
  },
  {
    path: ':currencyId/edit',
    component: CurrencyFormComponent,
    data: { mode: 'edit', title: 'page_titles.admin.currency_edit' },
  },
];