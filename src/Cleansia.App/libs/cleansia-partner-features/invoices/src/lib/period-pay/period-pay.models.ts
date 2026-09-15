import { TableColumn } from '@cleansia/components';
import { OrderEmployeePayDto } from '@cleansia/partner-services';

export type PeriodStatusKey = 'open' | 'closed' | 'paid' | 'unknown';

/**
 * The currency comes from the server and is never assumed here. On an invoiced period it is the
 * invoice's own currency, so this screen and the cleaner's payout document — which they file with
 * their tax return — read the same value. An absent code renders the amount with no symbol rather
 * than guessing one: no symbol is visibly incomplete, a wrong symbol is not.
 * → /flows/pay-and-payouts
 */
export function formatPayAmount(
  value: number | undefined,
  currencyCode: string | undefined
): string {
  return value !== undefined && value !== null
    ? `${value.toFixed(2)} ${currencyCode ?? ''}`.trimEnd()
    : '';
}

/**
 * A row's own currency first — the pay is in the ORDER's currency and the server names it per row —
 * and the summary's (the currency view) only for a row that carries none.
 */
export function getPeriodPayTableDefinition(currencyCode: string | undefined): {
  columns: TableColumn<OrderEmployeePayDto>[];
} {
  const format = (pay: OrderEmployeePayDto | undefined, value: number | undefined): string =>
    formatPayAmount(value, pay?.currencyCode ?? currencyCode);
  return {
    columns: [
      {
        id: 'orderNumber',
        field: 'orderNumber',
        header: 'pages.period_pay.order_number',
        sortable: false,
      },
      {
        id: 'basePay',
        field: 'basePay',
        header: 'pages.period_pay.base_pay',
        sortable: false,
        align: 'right',
        getValue: (pay?: OrderEmployeePayDto) => format(pay, pay?.basePay),
      },
      {
        id: 'extrasPay',
        field: 'extrasPay',
        header: 'pages.period_pay.extras_pay',
        sortable: false,
        align: 'right',
        getValue: (pay?: OrderEmployeePayDto) => format(pay, pay?.extrasPay),
      },
      {
        id: 'expensesPay',
        field: 'expensesPay',
        header: 'pages.period_pay.expenses_pay',
        sortable: false,
        align: 'right',
        getValue: (pay?: OrderEmployeePayDto) => format(pay, pay?.expensesPay),
      },
      {
        id: 'bonusPay',
        field: 'bonusPay',
        header: 'pages.period_pay.bonus_pay',
        sortable: false,
        align: 'right',
        getValue: (pay?: OrderEmployeePayDto) => format(pay, pay?.bonusPay),
      },
      {
        id: 'deductionPay',
        field: 'deductionPay',
        header: 'pages.period_pay.deduction_pay',
        sortable: false,
        align: 'right',
        getValue: (pay?: OrderEmployeePayDto) => format(pay, pay?.deductionPay),
      },
      {
        id: 'totalPay',
        field: 'totalPay',
        header: 'pages.period_pay.total_pay',
        sortable: false,
        align: 'right',
        getValue: (pay?: OrderEmployeePayDto) => format(pay, pay?.totalPay),
      },
    ],
  };
}
