import { Route } from '@angular/router';
import { AuditLogComponent } from './audit-log/audit-log.component';
import { ResourceHistoryComponent } from './resource-history/resource-history.component';

export const auditLogRoutes: Route[] = [
  {
    path: '',
    component: AuditLogComponent,
    data: { title: 'page_titles.admin.audit_log' },
  },
  {
    path: 'resource/:resourceType/:resourceId',
    component: ResourceHistoryComponent,
    data: { title: 'page_titles.admin.audit_log_history' },
  },
];
