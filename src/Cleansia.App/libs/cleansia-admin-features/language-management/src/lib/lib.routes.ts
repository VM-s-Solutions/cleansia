import { Route } from '@angular/router';
import { LanguageFormComponent } from './language-form/language-form.component';
import { LanguageManagementComponent } from './language-management/language-management.component';

export const languageManagementRoutes: Route[] = [
  {
    path: '',
    component: LanguageManagementComponent,
    data: { title: 'page_titles.admin.languages' },
  },
  {
    path: 'create',
    component: LanguageFormComponent,
    data: { mode: 'create', title: 'page_titles.admin.language_create' },
  },
  {
    path: ':languageId/edit',
    component: LanguageFormComponent,
    data: { mode: 'edit', title: 'page_titles.admin.language_edit' },
  },
];