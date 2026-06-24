import { CleansiaAdminRoute } from './routes.enum';

export const AuditResourceType = {
  Order: 'Order',
  Dispute: 'Dispute',
  AdminUser: 'AdminUser',
  EmployeePayConfig: 'EmployeePayConfig',
} as const;

export type AuditResourceType =
  (typeof AuditResourceType)[keyof typeof AuditResourceType];

export function buildAuditResourceHistoryRoute(
  resourceType: AuditResourceType,
  resourceId: string
): (string | CleansiaAdminRoute)[] {
  return [CleansiaAdminRoute.AUDIT_LOG, 'resource', resourceType, resourceId];
}
