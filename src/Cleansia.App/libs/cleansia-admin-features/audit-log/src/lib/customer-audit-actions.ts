import { ICleansiaSelectOption } from '@cleansia/components';
import { TranslateService } from '@ngx-translate/core';

// A copy of the backend roster ([AuditAction(Audience = AuditAudience.Customer)]), pinned against it
// and against the five locales by apps/cleansia-admin.app/src/app/i18n/customer-audit-action-catalogue.spec.ts.
export const CUSTOMER_AUDIT_ACTIONS = [
  'customer.order.create',
  'customer.order.cancel',
  'customer.order.recurring.confirm',
  'customer.dispute.create',
  'customer.account.register',
  'customer.consent.grant',
  'customer.consent.withdraw',
  'customer.gdpr.export',
  'customer.membership.subscribe',
  'customer.membership.swap',
  'customer.membership.cancel',
  'customer.notification_preferences.update',
  'customer.recurring.create',
  'customer.recurring.update',
  'customer.recurring.set_active',
  'customer.recurring.delete',
  'customer.session.login',
  'customer.session.logout',
  'customer.password.reset_requested',
  'customer.password.reset_completed',
  'customer.account.email_confirmed',
] as const;

// GetActionTimeline.EmployeeActionLabel — the two dotted labels the timeline gives employee rows.
export const EMPLOYEE_AUDIT_ACTIONS = [
  'employee.order.cover_requested',
  'employee.order.dropped',
  'employee.order.contract_accepted',
] as const;

const ACTION_LABEL_KEY_PREFIX = 'pages.audit_log.actions.';

const LABELLED_ACTIONS: ReadonlySet<string> = new Set<string>([
  ...CUSTOMER_AUDIT_ACTIONS,
  ...EMPLOYEE_AUDIT_ACTIONS,
]);

export function getAuditActionLabelKey(action: string | undefined): string | null {
  return action && LABELLED_ACTIONS.has(action)
    ? `${ACTION_LABEL_KEY_PREFIX}${action}`
    : null;
}

export function buildCustomerAuditActionOptions(
  translate: TranslateService
): ICleansiaSelectOption[] {
  return CUSTOMER_AUDIT_ACTIONS.map((action) => ({
    label: translate.instant(`${ACTION_LABEL_KEY_PREFIX}${action}`),
    value: action,
  }));
}
