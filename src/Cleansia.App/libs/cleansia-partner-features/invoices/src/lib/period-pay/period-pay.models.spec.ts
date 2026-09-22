import { OrderEmployeePayDto, PeriodPaySummaryDto } from '@cleansia/partner-services';
import {
  formatPayAmount,
  getPeriodCurrencies,
  getPeriodPayTableDefinition,
} from './period-pay.models';

describe('formatPayAmount', () => {
  it('formats an amount with two decimals the way the session language writes money', () => {
    expect(formatPayAmount(1234.5, 'CZK', 'cs')).toBe('1 234,50 Kč');
    expect(formatPayAmount(1234.5, 'CZK', 'en')).toBe('CZK 1,234.50');
  });

  it('formats zero', () => {
    expect(formatPayAmount(0, 'CZK', 'cs')).toBe('0,00 Kč');
  });

  it('returns an empty string for a missing amount', () => {
    expect(formatPayAmount(undefined, 'CZK', 'cs')).toBe('');
  });
});

describe('getPeriodPayTableDefinition', () => {
  it('defines the per-order pay line columns in pay-breakdown order', () => {
    const { columns } = getPeriodPayTableDefinition('CZK', 'cs');

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
    expect(formatPayAmount(1234.5, undefined, 'en')).toBe('1,234.50');
  });

  it('uses whatever the server sent, not a default', () => {
    expect(formatPayAmount(99, 'EUR', 'en')).toBe('€99.00');
  });

  // Every row now names its own currency. The summary's code is the fallback for a row that carries
  // none, never the other way round: a row's currency is the pay's, the summary's is the view's.
  it('labels a row with the currency the row carries before the summary one', () => {
    const { columns } = getPeriodPayTableDefinition('CZK', 'en');
    const total = columns.find((column) => column.id === 'totalPay');
    if (!total?.getValue) throw new Error('totalPay column missing or static');

    expect(total.getValue(OrderEmployeePayDto.fromJS({ totalPay: 99, currencyCode: 'EUR' }))).toBe('€99.00');
    expect(total.getValue(OrderEmployeePayDto.fromJS({ totalPay: 99 }))).toBe('CZK 99.00');
  });
});

describe('getPeriodCurrencies', () => {
  // The server lists the view currency first and then every other currency a pay row of the period
  // is in; this screen renders that list as it came and never re-derives it from invoices.
  it('maps the currencies the summary names, in the order the server sent them', () => {
    const summary = PeriodPaySummaryDto.fromJS({
      currencyCode: 'CZK',
      availableCurrencies: [
        { id: 'cur-czk', code: 'CZK' },
        { id: 'cur-eur', code: 'EUR' },
      ],
    });

    expect(getPeriodCurrencies(summary)).toEqual([
      { id: 'cur-czk', code: 'CZK' },
      { id: 'cur-eur', code: 'EUR' },
    ]);
  });

  it('skips an entry that names no id or no code', () => {
    const summary = PeriodPaySummaryDto.fromJS({
      availableCurrencies: [{ id: 'cur-czk' }, { code: 'EUR' }, { id: 'cur-usd', code: 'USD' }],
    });

    expect(getPeriodCurrencies(summary)).toEqual([{ id: 'cur-usd', code: 'USD' }]);
  });

  it('is empty for a summary from a server that does not name the currencies yet', () => {
    expect(getPeriodCurrencies(PeriodPaySummaryDto.fromJS({ currencyCode: 'CZK' }))).toEqual([]);
    expect(getPeriodCurrencies(null)).toEqual([]);
  });
});
