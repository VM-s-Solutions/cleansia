import { formatPayAmount, getPeriodPayTableDefinition } from './period-pay.models';

describe('formatPayAmount', () => {
  it('formats an amount with two decimals and the currency suffix', () => {
    expect(formatPayAmount(1234.5, 'CZK')).toBe('1234.50 CZK');
  });

  it('formats zero', () => {
    expect(formatPayAmount(0, 'CZK')).toBe('0.00 CZK');
  });

  it('returns an empty string for a missing amount', () => {
    expect(formatPayAmount(undefined, 'CZK')).toBe('');
  });
});

describe('getPeriodPayTableDefinition', () => {
  it('defines the per-order pay line columns in pay-breakdown order', () => {
    const { columns } = getPeriodPayTableDefinition('CZK');

    expect(columns.map((column) => column.id)).toEqual([
      'orderNumber',
      'basePay',
      'extrasPay',
      'expensesPay',
      'bonusPay',
      'deductionPay',
      'totalPay',
    ]);
  });

  it('renders the amount with no symbol when the server sent no currency', () => {
    // A missing code is visibly incomplete; guessing 'Kč' would be silently wrong the day a second
    // country configuration exists, which is the whole reason this argument exists.
    expect(formatPayAmount(1234.5, undefined)).toBe('1234.50');
  });

  it('uses whatever the server sent, not a default', () => {
    expect(formatPayAmount(99, 'EUR')).toBe('99.00 EUR');
  });
});
