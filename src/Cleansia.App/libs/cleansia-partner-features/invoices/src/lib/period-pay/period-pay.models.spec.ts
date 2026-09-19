import { OrderEmployeePayDto, PeriodPaySummaryDto } from '@cleansia/partner-services';
import {
  formatPayAmount,
  getPeriodCurrencies,
  getPeriodPayTableDefinition,
} from './period-pay.models';

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

  // Every row now names its own currency. The summary's code is the fallback for a row that carries
  // none, never the other way round: a row's currency is the pay's, the summary's is the view's.
  it('labels a row with the currency the row carries before the summary one', () => {
    const { columns } = getPeriodPayTableDefinition('CZK');
    const total = columns.find((column) => column.id === 'totalPay');
    if (!total?.getValue) throw new Error('totalPay column missing or static');

    expect(total.getValue(OrderEmployeePayDto.fromJS({ totalPay: 99, currencyCode: 'EUR' }))).toBe('99.00 EUR');
    expect(total.getValue(OrderEmployeePayDto.fromJS({ totalPay: 99 }))).toBe('99.00 CZK');
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
