import { TemplateRef } from '@angular/core';
import { CustomerActionAuditDto } from '@cleansia/admin-services';
import { FilterChip, TableAction, TableColumn } from '@cleansia/components';
import { TranslateService } from '@ngx-translate/core';
import { formatDate } from '@cleansia/utils';
import { formatTimestamp, getOutcomeLabelKey } from '../audit-log/audit-log.models';
import { getAuditActionLabelKey } from '../customer-audit-actions';
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
        getValue: (row: CustomerActionAuditDto) => formatTimestamp(row.occurredOn, translate.currentLang),
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

export interface CustomerAuditFilterValues {
  userId?: string | null;
  action?: string | null;
  resourceType?: string | null;
  resourceId?: string | null;
  clientAudience?: string | null;
  occurredFrom?: Date | null;
  occurredTo?: Date | null;
  success?: boolean | null;
}

export function buildCustomerAuditFilterChips(
  v: CustomerAuditFilterValues,
  translate: TranslateService
): FilterChip[] {
  const chips: FilterChip[] = [];
  const text = (key: string, value: string | null | undefined, labelKey: string) => {
    if (value) chips.push({ key, label: translate.instant(labelKey), value });
  };

  text('userId', v.userId, 'pages.audit_log.customers.filters.user_id');
  if (v.action) {
    const labelKey = getAuditActionLabelKey(v.action);
    chips.push({
      key: 'action',
      label: translate.instant('pages.audit_log.customers.filters.action'),
      value: labelKey ? translate.instant(labelKey) : v.action,
    });
  }
  text('resourceType', v.resourceType, 'pages.audit_log.filters.resource_type');
  text('resourceId', v.resourceId, 'pages.audit_log.filters.resource_id');
  text('clientAudience', v.clientAudience, 'pages.audit_log.customers.filters.audience');
  if (v.occurredFrom || v.occurredTo) {
    chips.push({
      key: 'dateRange',
      label: translate.instant('pages.audit_log.filters.date_range'),
      value: [v.occurredFrom, v.occurredTo]
        .filter(Boolean)
        .map((d) => formatDate(d as Date, translate.currentLang))
        .join(' – '),
      controls: ['occurredFrom', 'occurredTo'],
    });
  }
  if (v.success != null) {
    chips.push({
      key: 'success',
      label: translate.instant('pages.audit_log.filters.outcome'),
      value: translate.instant(getOutcomeLabelKey(v.success)),
    });
  }

  return chips;
}
