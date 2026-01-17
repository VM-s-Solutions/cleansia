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
    data: { title: 'page_titles.admin.templates' },
    children: [
      { path: '', redirectTo: 'invoice-templates', pathMatch: 'full' },
      {
        path: 'invoice-templates',
        component: InvoiceTemplateListComponent,
        data: { title: 'page_titles.admin.invoice_templates' },
      },
      {
        path: 'receipt-templates',
        component: ReceiptTemplateListComponent,
        data: { title: 'page_titles.admin.receipt_templates' },
      },
      {
        path: 'email-templates',
        component: EmailTemplateListComponent,
        data: { title: 'page_titles.admin.email_templates' },
      },
    ],
  },
  {
    path: 'invoice-templates/create',
    component: InvoiceTemplateFormComponent,
    data: { mode: 'create', title: 'page_titles.admin.invoice_template_create' },
  },
  {
    path: 'invoice-templates/:templateId/edit',
    component: InvoiceTemplateFormComponent,
    data: { mode: 'edit', title: 'page_titles.admin.invoice_template_edit' },
  },
  {
    path: 'receipt-templates/create',
    component: ReceiptTemplateFormComponent,
    data: { mode: 'create', title: 'page_titles.admin.receipt_template_create' },
  },
  {
    path: 'receipt-templates/:templateId/edit',
    component: ReceiptTemplateFormComponent,
    data: { mode: 'edit', title: 'page_titles.admin.receipt_template_edit' },
  },
  {
    path: 'email-templates/:emailType/translations',
    component: EmailTypeDetailComponent,
    data: { title: 'page_titles.admin.email_template_translations' },
  },
];
