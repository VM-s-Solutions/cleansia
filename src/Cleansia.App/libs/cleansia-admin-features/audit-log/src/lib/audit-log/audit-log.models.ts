import { TemplateRef } from '@angular/core';
import {
  ADMIN_ROLE_LABEL_KEYS,
  ADMIN_ROLES,
  AdminActionAuditDto,
  AdminRole,
  getAdminRoleLabelKey,
} from '@cleansia/admin-services';
import {
  FilterChip,
  ICleansiaSelectOption,
  TableAction,
  TableColumn,
} from '@cleansia/components';
import { formatDate } from '@cleansia/utils';
import { TranslateService } from '@ngx-translate/core';

export function getAuditLogTableColumns(
  translate: TranslateService,
  outcomeTemplate?: TemplateRef<AdminActionAuditDto>
): TableColumn<AdminActionAuditDto>[] {
  return [
    {
      id: 'occurredOn',
      numeric: true,
      field: 'occurredOn',
      header: translate.instant('pages.audit_log.columns.occurred_on'),
      sortable: true,
      width: '16%',
      getValue: (row: AdminActionAuditDto) => formatTimestamp(row.occurredOn, translate.currentLang),
    },
    {
      id: 'actor',
      field: 'actorEmail',
      header: translate.instant('pages.audit_log.columns.actor'),
      width: '20%',
      getValue: (row: AdminActionAuditDto) =>
        row.actorEmail ?? row.actorId ?? '',
    },
    {
      id: 'actorAdminRole',
      field: 'actorAdminRole',
      header: translate.instant('pages.audit_log.columns.actor_role'),
      width: '10%',
      getValue: (row: AdminActionAuditDto) => formatActorRole(row, translate),
    },
    {
      id: 'action',
      field: 'action',
      header: translate.instant('pages.audit_log.columns.action'),
      sortable: true,
      width: '18%',
      getValue: (row: AdminActionAuditDto) => row.action ?? '',
    },
    {
      id: 'resource',
      field: 'resourceType',
      header: translate.instant('pages.audit_log.columns.resource'),
      width: '16%',
      getValue: (row: AdminActionAuditDto) => formatResource(row),
    },
    {
      id: 'outcome',
      field: 'success',
      header: translate.instant('pages.audit_log.columns.outcome'),
      width: '10%',
      align: 'center',
      customTemplate: outcomeTemplate,
    },
  ];
}

export function getAuditLogTableActions(
  translate: TranslateService,
  onView: (row: AdminActionAuditDto) => void
): TableAction<AdminActionAuditDto>[] {
  return [
    {
      icon: 'pi pi-eye',
      color: 'primary',
      tooltip: translate.instant('pages.audit_log.entry.view'),
      visible: (row: AdminActionAuditDto) => !!row.id,
      onClick: onView,
    },
  ];
}

export function formatTimestamp(value: Date | undefined, lang: string | undefined): string {
  return formatDate(value, lang, 'dateTime');
}

export function formatResource(row: {
  resourceType?: string;
  resourceId?: string;
}): string {
  if (!row.resourceType) return '';
  return row.resourceId
    ? `${row.resourceType} · ${row.resourceId}`
    : row.resourceType;
}

export function getOutcomeLabelKey(success: boolean): string {
  return success
    ? 'pages.audit_log.outcome.success'
    : 'pages.audit_log.outcome.failure';
}

export function getOutcomeClass(success: boolean): string {
  return success ? 'status-badge status-badge--success' : 'status-badge status-badge--danger';
}

export function buildOutcomeOptions(
  translate: TranslateService
): ICleansiaSelectOption[] {
  return [
    {
      label: translate.instant('pages.audit_log.outcome.success'),
      value: true,
    },
    {
      label: translate.instant('pages.audit_log.outcome.failure'),
      value: false,
    },
  ];
}

export function formatActorRole(
  row: Pick<AdminActionAuditDto, 'actorAdminRole'>,
  translate: TranslateService
): string {
  const key = getAdminRoleLabelKey(row.actorAdminRole);
  return key ? translate.instant(key) : '';
}

export function buildActorRoleOptions(
  translate: TranslateService
): ICleansiaSelectOption[] {
  return ADMIN_ROLES.map((role) => ({
    label: translate.instant(ADMIN_ROLE_LABEL_KEYS[role]),
    value: role,
  }));
}

export interface AuditLogFilterValues {
  actorId?: string | null;
  actorEmail?: string | null;
  action?: string | null;
  resourceType?: string | null;
  resourceId?: string | null;
  occurredFrom?: Date | null;
  occurredTo?: Date | null;
  success?: boolean | null;
  actorAdminRole?: AdminRole | null;
}

export function buildAuditLogFilterChips(
  v: AuditLogFilterValues,
  translate: TranslateService
): FilterChip[] {
  const chips: FilterChip[] = [];
  const text = (key: string, value: string | null | undefined, labelKey: string) => {
    if (value) chips.push({ key, label: translate.instant(labelKey), value });
  };

  text('actorId', v.actorId, 'pages.audit_log.filters.actor_id');
  text('actorEmail', v.actorEmail, 'pages.audit_log.filters.actor_email');
  if (v.actorAdminRole != null) {
    chips.push({
      key: 'actorAdminRole',
      label: translate.instant('pages.audit_log.filters.actor_role'),
      value: formatActorRole({ actorAdminRole: v.actorAdminRole }, translate),
    });
  }
  text('action', v.action, 'pages.audit_log.filters.action');
  text('resourceType', v.resourceType, 'pages.audit_log.filters.resource_type');
  text('resourceId', v.resourceId, 'pages.audit_log.filters.resource_id');
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
