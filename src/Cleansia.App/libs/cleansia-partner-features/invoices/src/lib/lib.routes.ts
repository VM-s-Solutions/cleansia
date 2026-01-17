import { Route } from '@angular/router';
import { InvoiceDetailComponent } from './invoice-detail/invoice-detail.component';
import { InvoicesComponent } from './invoices/invoices.component';

export const invoicesRoutes: Route[] = [
  {
    path: '',
    component: InvoicesComponent,
    data: { title: 'page_titles.partner.invoices' },
  },
  {
    path: ':id',
    component: InvoiceDetailComponent,
    data: { title: 'page_titles.partner.invoice_details' },
  },
];
