import { Route } from '@angular/router';
import { AuditEntryComponent } from './audit-entry/audit-entry.component';
import { AuditLogComponent } from './audit-log/audit-log.component';
import { CustomerAuditEntryComponent } from './customer-audit-entry/customer-audit-entry.component';
import { CustomerAuditListComponent } from './customer-audit-list/customer-audit-list.component';
import { ResourceHistoryComponent } from './resource-history/resource-history.component';

export const auditLogRoutes: Route[] = [
  {
    path: '',
    component: AuditLogComponent,
    data: { title: 'page_titles.admin.audit_log' },
  },
  {
    path: 'customers/entry/:auditId',
    component: CustomerAuditEntryComponent,
    data: { title: 'page_titles.admin.audit_log_customer_entry' },
  },
  {
    path: 'customers',
    component: CustomerAuditListComponent,
    data: { title: 'page_titles.admin.audit_log_customers' },
  },
  {
    path: 'resource/:resourceType/:resourceId',
    component: ResourceHistoryComponent,
    data: { title: 'page_titles.admin.audit_log_history' },
  },
  {
    path: 'entry/:auditId',
    component: AuditEntryComponent,
    data: { title: 'page_titles.admin.audit_log_entry' },
  },
];
