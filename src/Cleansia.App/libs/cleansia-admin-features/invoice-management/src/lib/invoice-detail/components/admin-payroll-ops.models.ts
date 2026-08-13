export type AdminPayrollOpsPanel = 'adjust' | 'dispute' | 'reject';

export const PAYROLL_OPS_ERROR_KEY_MAP: Readonly<Record<string, string>> = {
  'payroll.invoice.not_found': 'api.payroll.invoice.not_found',
  'payroll.invoice.already_paid': 'api.payroll.invoice.already_paid',
  'payroll.invoice.invalid_status': 'api.payroll.invoice.invalid_status',
  'validation.must_be_positive': 'api.validation.must_be_positive',
  'common.required': 'api.common.required',
  'common.max_length': 'api.common.max_length',
};

export const PAYROLL_OPS_FALLBACK_ERROR_KEY = 'api.common.error_occurred';
