/**
 * Known status/enum name mappings for consistent snake_case conversion.
 * Handles common API values that may come with different casing.
 */
const KNOWN_SNAKE_CASE_MAPPINGS: Record<string, string> = {
  pending: 'pending',
  confirmed: 'confirmed',
  inprogress: 'in_progress',
  in_progress: 'in_progress',
  completed: 'completed',
  cancelled: 'cancelled',
  paid: 'paid',
  failed: 'failed',
  refunded: 'refunded',
  cash: 'cash',
  card: 'card',
};

/**
 * Converts a string to snake_case.
 * Handles PascalCase, camelCase, and known status/enum mappings.
 *
 * @example
 * toSnakeCase('InProgress') // 'in_progress'
 * toSnakeCase('paymentStatus') // 'payment_status'
 * toSnakeCase('Confirmed') // 'confirmed'
 */
export function toSnakeCase(str: string): string {
  const normalizedStr = str.toLowerCase().replace(/\s+/g, '');
  if (KNOWN_SNAKE_CASE_MAPPINGS[normalizedStr]) {
    return KNOWN_SNAKE_CASE_MAPPINGS[normalizedStr];
  }

  return str
    .replace(/([A-Z])/g, '_$1')
    .toLowerCase()
    .replace(/^_/, '')
    .replace(/\s+/g, '_');
}

/**
 * Converts a string to kebab-case.
 * Handles PascalCase and camelCase.
 *
 * @example
 * toKebabCase('InProgress') // 'in-progress'
 * toKebabCase('paymentStatus') // 'payment-status'
 */
export function toKebabCase(value?: string): string {
  if (!value) return '';
  return value
    .replace(/([a-z])([A-Z])/g, '$1-$2')
    .replace(/\s+/g, '-')
    .toLowerCase();
}
