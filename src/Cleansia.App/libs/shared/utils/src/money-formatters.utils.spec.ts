import { formatMoney } from './money-formatters.utils';

/** Digits only, so the assertion survives the locale's choice of grouping character. */
const digits = (s: string) => s.replace(/\D/g, '');

describe('formatMoney', () => {
  it('labels the amount with the currency it was given, not with crowns', () => {
    expect(formatMoney(1200, 'EUR', 'en-US')).toBe('€1,200');
    expect(formatMoney(1200, 'CZK', 'cs-CZ')).toBe('1 200 Kč');
  });

  it('drops the trailing zeroes a whole amount would otherwise carry', () => {
    expect(formatMoney(1200, 'CZK', 'cs-CZ')).not.toMatch(/[.,]\d/);
    expect(formatMoney(12, 'EUR', 'de-DE')).toBe('12 €');
  });

  it('keeps the cents of an amount that has them', () => {
    expect(formatMoney(12.5, 'EUR', 'en-US')).toBe('€12.50');
    expect(formatMoney(57.6, 'CZK', 'cs-CZ')).toBe('57,60 Kč');
  });

  it('treats binary dust as a whole amount', () => {
    expect(digits(formatMoney(57.999999, 'CZK', 'cs-CZ'))).toBe('58');
    expect(formatMoney(57.999999, 'CZK', 'cs-CZ')).not.toMatch(/[.,]\d/);
  });

  it('prints a bare number when the currency is not yet known rather than throwing', () => {
    expect(formatMoney(1200, null, 'en-US')).toBe('1,200');
    expect(formatMoney(12.5, undefined, 'en-US')).toBe('12.5');
    expect(formatMoney(1200, '', 'en-US')).toBe('1,200');
  });

  it('reads a non-number as zero', () => {
    expect(formatMoney(Number.NaN, 'EUR', 'en-US')).toBe('€0');
    expect(formatMoney(-0.001, 'EUR', 'en-US')).toBe('€0');
  });

  it('formats in the locale it was given', () => {
    expect(formatMoney(1234.5, 'EUR', 'de-DE')).toBe('1.234,50 €');
    expect(formatMoney(1234.5, 'EUR', 'en-GB')).toBe('€1,234.50');
  });
});
