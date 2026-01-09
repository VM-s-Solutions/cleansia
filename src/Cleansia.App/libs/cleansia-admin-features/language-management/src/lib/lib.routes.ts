import { Route } from '@angular/router';
import { LanguageFormComponent } from './language-form/language-form.component';
import { LanguageManagementComponent } from './language-management/language-management.component';

export const languageManagementRoutes: Route[] = [
  { path: '', component: LanguageManagementComponent },
  {
    path: 'create',
    component: LanguageFormComponent,
    data: { mode: 'create' },
  },
  {
    path: ':languageId/edit',
    component: LanguageFormComponent,
    data: { mode: 'edit' },
  },
];