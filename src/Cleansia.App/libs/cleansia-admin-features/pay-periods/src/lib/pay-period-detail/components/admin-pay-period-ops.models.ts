export type AdminPayPeriodOpsPanel = 'markPaid' | 'reopen';

export const PAY_PERIOD_OPS_ERROR_KEY_MAP: Readonly<Record<string, string>> = {
  'payroll.pay_period.not_found': 'api.payroll.pay_period.not_found',
  'pay_period.not_closed': 'api.pay_period.not_closed',
  'pay_period.already_paid': 'api.pay_period.already_paid',
  'common.required': 'api.common.required',
  'common.max_length': 'api.common.max_length',
};

export const PAY_PERIOD_OPS_FALLBACK_ERROR_KEY = 'api.common.error_occurred';
