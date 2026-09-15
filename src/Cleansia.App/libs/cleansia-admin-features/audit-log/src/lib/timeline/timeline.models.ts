import { TemplateRef } from '@angular/core';
import { TimelineEntryDto, TimelineSource } from '@cleansia/admin-services';
import { TableAction, TableColumn } from '@cleansia/components';
import { CleansiaAdminRoute } from '@cleansia/services';
import { TranslateService } from '@ngx-translate/core';
import { formatTimestamp } from '../audit-log/audit-log.models';
import { getAuditActionLabelKey } from '../customer-audit-actions';

export interface TimelineTemplates {
  source?: TemplateRef<TimelineEntryDto>;
  actor?: TemplateRef<TimelineEntryDto>;
  resource?: TemplateRef<TimelineEntryDto>;
  outcome?: TemplateRef<TimelineEntryDto>;
}

export function getTimelineTableDefinition(
  defs: { onView: (row: TimelineEntryDto) => void },
  translate: TranslateService,
  templates: TimelineTemplates
): {
  columns: TableColumn<TimelineEntryDto>[];
  actions: TableAction<TimelineEntryDto>[];
} {
  return {
    columns: [
      {
        id: 'occurredOn',
        field: 'occurredOn',
        header: translate.instant('pages.audit_log.timeline.columns.occurred_on'),
        width: '16%',
        getValue: (row: TimelineEntryDto) => formatTimestamp(row.occurredOn),
      },
      {
        id: 'source',
        field: 'source',
        header: translate.instant('pages.audit_log.timeline.columns.source'),
        width: '12%',
        customTemplate: templates.source,
      },
      // Only the resource history shows who acted: on a customer's own page the actor is the page.
      ...(templates.actor
        ? [
            {
              id: 'actor',
              field: 'actorId',
              header: translate.instant('pages.audit_log.timeline.columns.actor'),
              width: '16%',
              customTemplate: templates.actor,
            },
          ]
        : []),
      {
        id: 'action',
        field: 'action',
        header: translate.instant('pages.audit_log.timeline.columns.action'),
        width: '24%',
        getValue: (row: TimelineEntryDto) => formatActionLabel(row.action, translate),
      },
      {
        id: 'resource',
        field: 'resourceType',
        header: translate.instant('pages.audit_log.timeline.columns.resource'),
        width: '20%',
        customTemplate: templates.resource,
      },
      {
        id: 'outcome',
        field: 'success',
        header: translate.instant('pages.audit_log.timeline.columns.outcome'),
        width: '12%',
        customTemplate: templates.outcome,
      },
    ],
    actions: [
      {
        icon: 'pi pi-eye',
        color: 'primary',
        tooltip: translate.instant('pages.audit_log.timeline.view'),
        visible: hasTimelineEntryDetail,
        onClick: defs.onView,
      },
    ],
  };
}

export function formatActionLabel(
  action: string | undefined,
  translate: TranslateService
): string {
  const key = getAuditActionLabelKey(action);
  return key ? translate.instant(key) : action ?? '';
}

export function hasTimelineEntryDetail(entry: TimelineEntryDto): boolean {
  return buildTimelineEntryRoute(entry) !== null;
}

export function buildTimelineEntryRoute(
  entry: TimelineEntryDto
): (string | CleansiaAdminRoute)[] | null {
  if (!entry.id) return null;
  switch (entry.source) {
    case TimelineSource.Customer:
      return [CleansiaAdminRoute.AUDIT_LOG, 'customers', 'entry', entry.id];
    case TimelineSource.Admin:
      return [CleansiaAdminRoute.AUDIT_LOG, 'entry', entry.id];
    default:
      return null;
  }
}

/**
 * A customer row's actor is a customer, and the customer page holds their whole trail — the entry
 * point from an order's or a dispute's history to the person (ADR-0062 D6). Admin and cleaner
 * actors have their own tables; the row only names them.
 */
export function buildTimelineActorRoute(entry: TimelineEntryDto): string[] | null {
  return entry.source === TimelineSource.Customer && entry.actorId
    ? ['/customers', entry.actorId]
    : null;
}

export function getTimelineSourceLabelKey(source: TimelineSource): string {
  switch (source) {
    case TimelineSource.Customer:
      return 'pages.audit_log.timeline.source.customer';
    case TimelineSource.Admin:
      return 'pages.audit_log.timeline.source.admin';
    case TimelineSource.Employee:
      return 'pages.audit_log.timeline.source.employee';
    default:
      return '';
  }
}

export function getTimelineSourceClass(source: TimelineSource): string {
  switch (source) {
    case TimelineSource.Customer:
      return 'audit-source-badge source-customer';
    case TimelineSource.Admin:
      return 'audit-source-badge source-admin';
    case TimelineSource.Employee:
      return 'audit-source-badge source-employee';
    default:
      return 'audit-source-badge';
  }
}
