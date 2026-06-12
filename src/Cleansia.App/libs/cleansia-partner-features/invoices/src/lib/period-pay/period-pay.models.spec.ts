import { formatPayAmount, getPeriodPayTableDefinition } from './period-pay.models';

describe('formatPayAmount', () => {
  it('formats an amount with two decimals and the currency suffix', () => {
    expect(formatPayAmount(1234.5)).toBe('1234.50 Kč');
  });

  it('formats zero', () => {
    expect(formatPayAmount(0)).toBe('0.00 Kč');
  });

  it('returns an empty string for a missing amount', () => {
    expect(formatPayAmount(undefined)).toBe('');
  });
});

describe('getPeriodPayTableDefinition', () => {
  it('defines the per-order pay line columns in pay-breakdown order', () => {
    const { columns } = getPeriodPayTableDefinition();

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
});
