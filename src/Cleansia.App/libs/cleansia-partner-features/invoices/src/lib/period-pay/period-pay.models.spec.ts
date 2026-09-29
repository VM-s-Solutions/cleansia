import { readFileSync } from 'fs';
import { join } from 'path';
import { CashHeldDto, OrderEmployeePayDto, PayLineType, PeriodPaySummaryDto } from '@cleansia/partner-services';
import { TranslateService } from '@ngx-translate/core';
import { getOrderPaysTableDefinition } from '../invoice-detail/invoice-detail.models';
import {
  buildCashHeldRows,
  formatPayAmount,
  getPeriodCurrencies,
  getPeriodPayTableDefinition,
} from './period-pay.models';

const translate = { instant: (key: string) => key } as unknown as TranslateService;

const PARTNER_LOCALES = ['en', 'cs', 'sk', 'uk', 'ru'] as const;
const I18N_DIR = join(__dirname, '../../../../../../apps/cleansia-partner.app/src/assets/i18n');

function missingIn(locale: string, keys: string[]): string[] {
  const bundle = JSON.parse(readFileSync(join(I18N_DIR, `${locale}.json`), 'utf8')) as unknown;
  return keys.filter((key) => {
    const value = key
      .split('.')
      .reduce<unknown>(
        (node, segment) => (node && typeof node === 'object' ? (node as Record<string, unknown>)[segment] : undefined),
        bundle
      );
    return typeof value !== 'string' || !value.trim();
  });
}

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
    const { columns } = getPeriodPayTableDefinition('CZK', 'cs', translate);

    expect(columns.map((column) => column.id)).toEqual([
      'orderNumber',
      'lineType',
      'basePay',
      'extrasPay',
      'dirtinessPay',
      'expensesPay',
      'bonusPay',
      'deductionPay',
      'deductionReason',
      'totalPay',
    ]);
  });

  // A deduction charged to the partner when a dispute found them at fault carries the reason the
  // administrator wrote; the partner reads it beside the amount.
  it('prints the reason given for a deduction, and nothing for a row without one', () => {
    const { columns } = getPeriodPayTableDefinition('CZK', 'cs', translate);
    const reason = columns.find((column) => column.id === 'deductionReason');
    if (!reason?.getValue) throw new Error('deductionReason column missing or static');

    expect(
      reason.getValue(OrderEmployeePayDto.fromJS({ deductionPay: 200, deductionReason: 'Kitchen left uncleaned' }))
    ).toBe('Kitchen left uncleaned');
    expect(reason.getValue(OrderEmployeePayDto.fromJS({ deductionPay: 0 }))).toBe('');
  });

  it.each(PARTNER_LOCALES)('names the reason column of the pay lines and of the invoice in %s', (locale) => {
    const headers = [getPeriodPayTableDefinition('CZK', 'cs', translate), getOrderPaysTableDefinition('CZK', 'cs')].map(
      ({ columns }) => columns.find((column) => column.id === 'deductionReason')?.header ?? ''
    );

    expect(missingIn(locale, headers)).toEqual([]);
  });

  it('prints the dirtiness term of a row in the row currency', () => {
    const { columns } = getPeriodPayTableDefinition('CZK', 'en', translate);
    const dirtiness = columns.find((column) => column.id === 'dirtinessPay');
    if (!dirtiness?.getValue) throw new Error('dirtinessPay column missing or static');

    expect(dirtiness.getValue(OrderEmployeePayDto.fromJS({ dirtinessPay: 195, currencyCode: 'EUR' }))).toBe('€195.00');
  });

  it.each(PARTNER_LOCALES)('names the dirtiness column of the pay lines and of the invoice in %s', (locale) => {
    const headers = [getPeriodPayTableDefinition('CZK', 'cs', translate), getOrderPaysTableDefinition('CZK', 'cs')].map(
      ({ columns }) => columns.find((column) => column.id === 'dirtinessPay')?.header ?? ''
    );

    expect(missingIn(locale, headers)).toEqual([]);
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
    const { columns } = getPeriodPayTableDefinition('CZK', 'en', translate);
    const total = columns.find((column) => column.id === 'totalPay');
    if (!total?.getValue) throw new Error('totalPay column missing or static');

    expect(total.getValue(OrderEmployeePayDto.fromJS({ totalPay: 99, currencyCode: 'EUR' }))).toBe('€99.00');
    expect(total.getValue(OrderEmployeePayDto.fromJS({ totalPay: 99 }))).toBe('CZK 99.00');
  });
});

// Owner ruling 2026-09-28, decision 16: the crew is paid a share of a late-cancellation or lockout fee once it
// is collected, as a pay line of its own type; the cleaner reads which fee a line shares.
describe('the kind of each pay line', () => {
  const lineOf = (lineType: PayLineType) =>
    getPeriodPayTableDefinition('CZK', 'en', translate)
      .columns.find((column) => column.id === 'lineType')
      ?.getValue?.(OrderEmployeePayDto.fromJS({ orderNumber: 'CL-1001', lineType }));

  it('names a job and each share of a collected fee', () => {
    expect(lineOf(PayLineType.Job)).toBe('enums.pay_line_type.job');
    expect(lineOf(PayLineType.CancellationFeeShare)).toBe('enums.pay_line_type.cancellation_fee_share');
    expect(lineOf(PayLineType.LockoutFeeShare)).toBe('enums.pay_line_type.lockout_fee_share');
  });

  it('prints a dash for a line type the screen does not know yet rather than a machine key', () => {
    expect(lineOf(99 as PayLineType)).toBe('-');
  });

  it.each(PARTNER_LOCALES)('words the column and every line type in %s', (locale) => {
    expect(
      missingIn(locale, [
        'pages.period_pay.line_type',
        'enums.pay_line_type.job',
        'enums.pay_line_type.cancellation_fee_share',
        'enums.pay_line_type.lockout_fee_share',
      ])
    ).toEqual([]);
  });
});

// Owner ruling 2026-09-28, decisions 23 and 25: the cleaner reads the company cash they hold per currency,
// the company's float cap when one is set, and that cash jobs are hidden above it.
describe('buildCashHeldRows', () => {
  it('states each currency the cleaner holds to the cent, with the cap and whether cash jobs are hidden', () => {
    const rows = buildCashHeldRows(
      [
        CashHeldDto.fromJS({ currencyId: 'cur-czk', currencyCode: 'CZK', amount: 5200.5, floatCap: 5000, cashJobsHidden: true }),
        CashHeldDto.fromJS({ currencyId: 'cur-eur', currencyCode: 'EUR', amount: 40, cashJobsHidden: false }),
      ],
      'en'
    );

    expect(rows).toEqual([
      { currencyId: 'cur-czk', amount: 'CZK 5,200.50', limit: 'CZK 5,000.00', cashJobsHidden: true },
      { currencyId: 'cur-eur', amount: '€40.00', limit: null, cashJobsHidden: false },
    ]);
  });

  it('is empty for a cleaner who holds no company cash', () => {
    expect(buildCashHeldRows([], 'cs')).toEqual([]);
  });

  it.each(PARTNER_LOCALES)('words the cash I hold section in %s', (locale) => {
    expect(
      missingIn(locale, [
        'pages.period_pay.cash_held.title',
        'pages.period_pay.cash_held.description',
        'pages.period_pay.cash_held.none',
        'pages.period_pay.cash_held.load_error',
        'pages.period_pay.cash_held.held',
        'pages.period_pay.cash_held.limit',
        'pages.period_pay.cash_held.jobs_hidden',
      ])
    ).toEqual([]);
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
