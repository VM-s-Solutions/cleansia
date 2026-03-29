import { Route } from '@angular/router';
import { EmailTemplateListComponent } from './email-template-list/email-template-list.component';
import { EmailTypeDetailComponent } from './email-type-detail/email-type-detail.component';
import { TemplateManagementComponent } from './template-management.component';

export const templateManagementRoutes: Route[] = [
  {
    path: '',
    component: TemplateManagementComponent,
    data: { title: 'page_titles.admin.templates' },
    children: [
      { path: '', redirectTo: 'email-templates', pathMatch: 'full' },
      {
        path: 'email-templates',
        component: EmailTemplateListComponent,
        data: { title: 'page_titles.admin.email_templates' },
      },
    ],
  },
  {
    path: 'email-templates/:emailType/translations',
    component: EmailTypeDetailComponent,
    data: { title: 'page_titles.admin.email_template_translations' },
  },
];
