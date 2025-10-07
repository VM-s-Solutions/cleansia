import { Route } from '@angular/router';
import { InvoiceDetailComponent } from './invoice-detail/invoice-detail.component';
import { InvoicesComponent } from './invoices/invoices.component';

export const invoicesRoutes: Route[] = [
  { path: '', component: InvoicesComponent },
  { path: ':id', component: InvoiceDetailComponent },
];
