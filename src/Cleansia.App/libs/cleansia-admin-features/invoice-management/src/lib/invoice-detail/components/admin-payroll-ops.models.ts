export type AdminPayrollOpsPanel = 'adjust' | 'dispute' | 'reject';

export const PAYROLL_OPS_ERROR_KEY_MAP: Readonly<Record<string, string>> = {
  'payroll.invoice.not_found': 'errors.payroll.invoice.not_found',
  'payroll.invoice.already_paid': 'errors.payroll.invoice.already_paid',
  'payroll.invoice.invalid_status': 'errors.payroll.invoice.invalid_status',
  'validation.must_be_positive': 'errors.validation.must_be_positive',
  'common.required': 'errors.common.required',
  'common.max_length': 'errors.common.max_length',
};

export const PAYROLL_OPS_FALLBACK_ERROR_KEY = 'errors.common.error_occurred';
