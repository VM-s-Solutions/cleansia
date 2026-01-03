import { Route } from '@angular/router';
import { InvoiceManagementComponent } from './invoice-management/invoice-management.component';
import { InvoiceDetailComponent } from './invoice-detail/invoice-detail.component';

export const invoiceManagementRoutes: Route[] = [
  { path: '', component: InvoiceManagementComponent },
  { path: ':invoiceId', component: InvoiceDetailComponent },
];
