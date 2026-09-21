import { TemplateRef } from '@angular/core';
import {
  ADMIN_ROLE_LABEL_KEYS,
  ADMIN_ROLES,
  AdminActionAuditDto,
  getAdminRoleLabelKey,
} from '@cleansia/admin-services';
import {
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
  return success
    ? 'audit-outcome-badge outcome-success'
    : 'audit-outcome-badge outcome-failure';
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
