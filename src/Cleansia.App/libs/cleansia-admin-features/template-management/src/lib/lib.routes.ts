import { Route } from '@angular/router';
import { InvoiceTemplateListComponent } from './invoice-template-list/invoice-template-list.component';
import { InvoiceTemplateFormComponent } from './invoice-template-form/invoice-template-form.component';
import { ReceiptTemplateListComponent } from './receipt-template-list/receipt-template-list.component';
import { ReceiptTemplateFormComponent } from './receipt-template-form/receipt-template-form.component';
import { EmailTemplateListComponent } from './email-template-list/email-template-list.component';
import { EmailTypeDetailComponent } from './email-type-detail/email-type-detail.component';
import { TemplateManagementComponent } from './template-management.component';

export const templateManagementRoutes: Route[] = [
  {
    path: '',
    component: TemplateManagementComponent,
    children: [
      { path: '', redirectTo: 'invoice-templates', pathMatch: 'full' },
      { path: 'invoice-templates', component: InvoiceTemplateListComponent },
      { path: 'receipt-templates', component: ReceiptTemplateListComponent },
      { path: 'email-templates', component: EmailTemplateListComponent },
    ],
  },
  {
    path: 'invoice-templates/create',
    component: InvoiceTemplateFormComponent,
    data: { mode: 'create' },
  },
  {
    path: 'invoice-templates/:templateId/edit',
    component: InvoiceTemplateFormComponent,
    data: { mode: 'edit' },
  },
  {
    path: 'receipt-templates/create',
    component: ReceiptTemplateFormComponent,
    data: { mode: 'create' },
  },
  {
    path: 'receipt-templates/:templateId/edit',
    component: ReceiptTemplateFormComponent,
    data: { mode: 'edit' },
  },
  {
    path: 'email-templates/:emailType/translations',
    component: EmailTypeDetailComponent,
  },
];
