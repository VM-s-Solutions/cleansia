export type AdminPayPeriodOpsPanel = 'markPaid' | 'reopen';

export const PAY_PERIOD_OPS_ERROR_KEY_MAP: Readonly<Record<string, string>> = {
  'payroll.pay_period.not_found': 'errors.payroll.pay_period.not_found',
  'pay_period.not_closed': 'errors.pay_period.not_closed',
  'pay_period.already_paid': 'errors.pay_period.already_paid',
  'common.required': 'errors.common.required',
  'common.max_length': 'errors.common.max_length',
};

export const PAY_PERIOD_OPS_FALLBACK_ERROR_KEY = 'errors.common.error_occurred';
