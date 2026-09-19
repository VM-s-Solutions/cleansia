import { Route } from '@angular/router';
import { LegalDocumentsComponent } from './legal-documents/legal-documents.component';

export const legalDocumentsRoutes: Route[] = [
  {
    path: '',
    component: LegalDocumentsComponent,
    data: { title: 'page_titles.admin.legal_documents' },
  },
];
