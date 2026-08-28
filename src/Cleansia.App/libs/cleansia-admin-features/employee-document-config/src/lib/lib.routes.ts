import { Route } from '@angular/router';
import { DeletionRequestsComponent } from './deletion-requests/deletion-requests.component';
import { DocumentRequirementsComponent } from './document-requirements/document-requirements.component';

/**
 * Two sibling screens over the same feature: the rules a country expects, and the queue of
 * cleaners asking for a document to be removed. Both read with `CanViewEmployeeDocuments`; the
 * actions inside them are gated separately, because writing a rule and answering a request are
 * different permissions on the server.
 */
export const employeeDocumentConfigRoutes: Route[] = [
  {
    path: '',
    component: DocumentRequirementsComponent,
    data: { title: 'page_titles.admin.document_requirements' },
  },
  {
    path: 'deletion-requests',
    component: DeletionRequestsComponent,
    data: { title: 'page_titles.admin.document_deletion_requests' },
  },
];
