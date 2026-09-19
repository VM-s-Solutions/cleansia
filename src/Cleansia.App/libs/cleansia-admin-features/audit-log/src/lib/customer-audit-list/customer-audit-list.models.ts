import { TemplateRef } from '@angular/core';
import { CustomerActionAuditDto } from '@cleansia/admin-services';
import { TableAction, TableColumn } from '@cleansia/components';
import { TranslateService } from '@ngx-translate/core';
import { formatTimestamp } from '../audit-log/audit-log.models';
import { formatActionLabel } from '../timeline/timeline.models';

export interface CustomerAuditTemplates {
  user?: TemplateRef<CustomerActionAuditDto>;
  resource?: TemplateRef<CustomerActionAuditDto>;
  outcome?: TemplateRef<CustomerActionAuditDto>;
}

export function getCustomerAuditTableDefinition(
  defs: { onView: (row: CustomerActionAuditDto) => void },
  translate: TranslateService,
  templates: CustomerAuditTemplates
): {
  columns: TableColumn<CustomerActionAuditDto>[];
  actions: TableAction<CustomerActionAuditDto>[];
} {
  return {
    columns: [
      {
        id: 'occurredOn',
        field: 'occurredOn',
        header: translate.instant('pages.audit_log.customers.columns.occurred_on'),
        sortable: true,
        width: '14%',
        getValue: (row: CustomerActionAuditDto) => formatTimestamp(row.occurredOn),
      },
      {
        id: 'user',
        field: 'userId',
        header: translate.instant('pages.audit_log.customers.columns.user'),
        sortable: true,
        width: '18%',
        customTemplate: templates.user,
      },
      {
        id: 'action',
        field: 'action',
        header: translate.instant('pages.audit_log.customers.columns.action'),
        sortable: true,
        width: '22%',
        getValue: (row: CustomerActionAuditDto) => formatActionLabel(row.action, translate),
      },
      {
        id: 'resource',
        field: 'resourceType',
        header: translate.instant('pages.audit_log.customers.columns.resource'),
        width: '20%',
        customTemplate: templates.resource,
      },
      {
        id: 'clientAudience',
        field: 'clientAudience',
        header: translate.instant('pages.audit_log.customers.columns.audience'),
        width: '12%',
        getValue: (row: CustomerActionAuditDto) => row.clientAudience ?? '',
      },
      {
        id: 'outcome',
        field: 'success',
        header: translate.instant('pages.audit_log.customers.columns.outcome'),
        width: '10%',
        customTemplate: templates.outcome,
      },
    ],
    actions: [
      {
        icon: 'pi pi-eye',
        color: 'primary',
        tooltip: translate.instant('pages.audit_log.customers.entry.view'),
        visible: (row: CustomerActionAuditDto) => !!row.id,
        onClick: defs.onView,
      },
    ],
  };
}
