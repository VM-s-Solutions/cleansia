import { TableColumn } from '@cleansia/components';
import {
  CashHeldDto,
  OrderEmployeePayDto,
  PayLineType,
  PeriodPaySummaryDto,
} from '@cleansia/partner-services';
import { formatMoney, localeFor } from '@cleansia/utils';
import { TranslateService } from '@ngx-translate/core';

export const PAY_LINE_TYPE_LABEL_KEYS: Readonly<Record<PayLineType, string>> = {
  [PayLineType.Job]: 'enums.pay_line_type.job',
  [PayLineType.CancellationFeeShare]: 'enums.pay_line_type.cancellation_fee_share',
  [PayLineType.LockoutFeeShare]: 'enums.pay_line_type.lockout_fee_share',
};

export interface CashHeldRow {
  currencyId: string | undefined;
  amount: string;
  limit: string | null;
  cashJobsHidden: boolean;
}

export interface PeriodCurrency {
  id: string;
  code: string;
}

/**
 * The currencies a period can be viewed in come with the summary: the view currency first, then
 * every other currency a pay row of the period is in. The server answers GetPeriodPays in ONE
 * currency, so this list is the only way a cleaner paid in two reaches the second one's rows.
 * It is not derived from invoices: an open period has none, and a cancelled one is a document
 * over rows that may no longer be there.
 */
export function getPeriodCurrencies(summary: PeriodPaySummaryDto | null): PeriodCurrency[] {
  return (summary?.availableCurrencies ?? []).flatMap((currency) =>
    currency.id && currency.code ? [{ id: currency.id, code: currency.code }] : []
  );
}

/**
 * The currency comes from the server and is never assumed here. On an invoiced period it is the
 * invoice's own currency, so this screen and the cleaner's payout document — which they file with
 * their tax return — read the same value. An absent code renders the amount with no symbol rather
 * than guessing one: no symbol is visibly incomplete, a wrong symbol is not.
 * → /flows/pay-and-payouts
 */
export function formatPayAmount(
  value: number | undefined,
  currencyCode: string | undefined,
  lang: string | undefined
): string {
  return value !== undefined && value !== null
    ? formatMoney(value, currencyCode || undefined, localeFor(lang), { fractionDigits: 2 })
    : '';
}

/**
 * A row's own currency first — the pay is in the ORDER's currency and the server names it per row —
 * and the summary's (the currency view) only for a row that carries none.
 */
export function getPeriodPayTableDefinition(
  currencyCode: string | undefined,
  lang: string | undefined,
  translate: TranslateService
): {
  columns: TableColumn<OrderEmployeePayDto>[];
} {
  const format = (pay: OrderEmployeePayDto | undefined, value: number | undefined): string =>
    formatPayAmount(value, pay?.currencyCode ?? currencyCode, lang);
  return {
    columns: [
      {
        id: 'orderNumber',
        field: 'orderNumber',
        header: 'pages.period_pay.order_number',
        sortable: false,
      },
      {
        id: 'lineType',
        field: 'lineType',
        header: 'pages.period_pay.line_type',
        sortable: false,
        getValue: (pay?: OrderEmployeePayDto) => {
          const key = pay ? PAY_LINE_TYPE_LABEL_KEYS[pay.lineType] : undefined;
          return key ? translate.instant(key) : '-';
        },
      },
      {
        id: 'basePay',
        field: 'basePay',
        header: 'pages.period_pay.base_pay',
        sortable: false,
        numeric: true,
        getValue: (pay?: OrderEmployeePayDto) => format(pay, pay?.basePay),
      },
      {
        id: 'extrasPay',
        field: 'extrasPay',
        header: 'pages.period_pay.extras_pay',
        sortable: false,
        numeric: true,
        getValue: (pay?: OrderEmployeePayDto) => format(pay, pay?.extrasPay),
      },
      {
        id: 'dirtinessPay',
        field: 'dirtinessPay',
        header: 'pages.period_pay.dirtiness_pay',
        sortable: false,
        numeric: true,
        getValue: (pay?: OrderEmployeePayDto) => format(pay, pay?.dirtinessPay),
      },
      {
        id: 'expensesPay',
        field: 'expensesPay',
        header: 'pages.period_pay.expenses_pay',
        sortable: false,
        numeric: true,
        getValue: (pay?: OrderEmployeePayDto) => format(pay, pay?.expensesPay),
      },
      {
        id: 'bonusPay',
        field: 'bonusPay',
        header: 'pages.period_pay.bonus_pay',
        sortable: false,
        numeric: true,
        getValue: (pay?: OrderEmployeePayDto) => format(pay, pay?.bonusPay),
      },
      {
        id: 'deductionPay',
        field: 'deductionPay',
        header: 'pages.period_pay.deduction_pay',
        sortable: false,
        numeric: true,
        getValue: (pay?: OrderEmployeePayDto) => format(pay, pay?.deductionPay),
      },
      {
        id: 'deductionReason',
        field: 'deductionReason',
        header: 'pages.period_pay.deduction_reason',
        sortable: false,
        getValue: (pay?: OrderEmployeePayDto) => pay?.deductionReason ?? '',
      },
      {
        id: 'totalPay',
        field: 'totalPay',
        header: 'pages.period_pay.total_pay',
        sortable: false,
        numeric: true,
        getValue: (pay?: OrderEmployeePayDto) => format(pay, pay?.totalPay),
      },
    ],
  };
}

export function buildCashHeldRows(held: CashHeldDto[], lang: string | undefined): CashHeldRow[] {
  return held.map((balance) => ({
    currencyId: balance.currencyId,
    amount: formatPayAmount(balance.amount, balance.currencyCode, lang),
    limit:
      balance.floatCap === undefined || balance.floatCap === null
        ? null
        : formatPayAmount(balance.floatCap, balance.currencyCode, lang),
    cashJobsHidden: balance.cashJobsHidden,
  }));
}
