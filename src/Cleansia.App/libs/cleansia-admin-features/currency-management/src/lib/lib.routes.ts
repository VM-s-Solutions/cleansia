import { Route } from '@angular/router';
import { CurrencyFormComponent } from './currency-form/currency-form.component';
import { CurrencyManagementComponent } from './currency-management/currency-management.component';

export const currencyManagementRoutes: Route[] = [
  { path: '', component: CurrencyManagementComponent },
  {
    path: 'create',
    component: CurrencyFormComponent,
    data: { mode: 'create' },
  },
  {
    path: ':currencyId/edit',
    component: CurrencyFormComponent,
    data: { mode: 'edit' },
  },
];