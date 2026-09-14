import {
  CUSTOMER_AUDIT_ACTIONS,
  EMPLOYEE_AUDIT_ACTIONS,
  buildCustomerAuditActionOptions,
  getAuditActionLabelKey,
} from './customer-audit-actions';

describe('customer audit action catalogue', () => {
  it('carries the sixteen customer labels and the two employee labels, each once', () => {
    expect(new Set(CUSTOMER_AUDIT_ACTIONS).size).toBe(16);
    expect(new Set(EMPLOYEE_AUDIT_ACTIONS).size).toBe(2);
    for (const action of [...CUSTOMER_AUDIT_ACTIONS, ...EMPLOYEE_AUDIT_ACTIONS]) {
      expect(action).toMatch(/^(customer|employee)\.[a-z_]+(\.[a-z_]+)+$/);
    }
  });

  it('resolves a catalogued label to its i18n key under pages.audit_log.actions', () => {
    expect(getAuditActionLabelKey('customer.order.cancel')).toBe(
      'pages.audit_log.actions.customer.order.cancel'
    );
    expect(getAuditActionLabelKey('employee.order.dropped')).toBe(
      'pages.audit_log.actions.employee.order.dropped'
    );
  });

  it('returns null for an admin action or an unknown label so the raw action is shown instead', () => {
    expect(getAuditActionLabelKey('IssuePartialRefund')).toBeNull();
    expect(getAuditActionLabelKey('gdpr.user.export')).toBeNull();
    expect(getAuditActionLabelKey(undefined)).toBeNull();
    expect(getAuditActionLabelKey('')).toBeNull();
  });

  it('builds one select option per customer label, valued by the raw label the filter sends', () => {
    const translate = { instant: (k: string) => `t:${k}` };
    const options = buildCustomerAuditActionOptions(
      translate as unknown as import('@ngx-translate/core').TranslateService
    );

    expect(options.map((o) => o.value)).toEqual([...CUSTOMER_AUDIT_ACTIONS]);
    expect(options[0].label).toBe(
      `t:pages.audit_log.actions.${CUSTOMER_AUDIT_ACTIONS[0]}`
    );
  });
});
