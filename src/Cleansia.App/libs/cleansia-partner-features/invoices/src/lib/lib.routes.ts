import { Route } from '@angular/router';
import { InvoiceDetailComponent } from './invoice-detail/invoice-detail.component';
import { InvoicesComponent } from './invoices/invoices.component';
import { PeriodPayComponent } from './period-pay/period-pay.component';

export const invoicesRoutes: Route[] = [
  {
    path: '',
    component: InvoicesComponent,
    data: { title: 'page_titles.partner.invoices' },
  },
  {
    path: ':invoiceId',
    component: InvoiceDetailComponent,
    data: { title: 'page_titles.partner.invoice_details' },
  },
];

export const periodPayRoutes: Route[] = [
  {
    path: '',
    component: PeriodPayComponent,
    data: { title: 'page_titles.partner.my_pay' },
  },
];
