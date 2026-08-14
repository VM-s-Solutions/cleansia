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

export function getPeriodPayTableDefinition(currencyCode: string | undefined): {
  columns: TableColumn<OrderEmployeePayDto>[];
} {
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
        getValue: (pay?: OrderEmployeePayDto) => formatPayAmount(pay?.basePay, currencyCode),
      },
      {
        id: 'extrasPay',
        field: 'extrasPay',
        header: 'pages.period_pay.extras_pay',
        sortable: false,
        align: 'right',
        getValue: (pay?: OrderEmployeePayDto) => formatPayAmount(pay?.extrasPay, currencyCode),
      },
      {
        id: 'expensesPay',
        field: 'expensesPay',
        header: 'pages.period_pay.expenses_pay',
        sortable: false,
        align: 'right',
        getValue: (pay?: OrderEmployeePayDto) => formatPayAmount(pay?.expensesPay, currencyCode),
      },
      {
        id: 'bonusPay',
        field: 'bonusPay',
        header: 'pages.period_pay.bonus_pay',
        sortable: false,
        align: 'right',
        getValue: (pay?: OrderEmployeePayDto) => formatPayAmount(pay?.bonusPay, currencyCode),
      },
      {
        id: 'deductionPay',
        field: 'deductionPay',
        header: 'pages.period_pay.deduction_pay',
        sortable: false,
        align: 'right',
        getValue: (pay?: OrderEmployeePayDto) => formatPayAmount(pay?.deductionPay, currencyCode),
      },
      {
        id: 'totalPay',
        field: 'totalPay',
        header: 'pages.period_pay.total_pay',
        sortable: false,
        align: 'right',
        getValue: (pay?: OrderEmployeePayDto) => formatPayAmount(pay?.totalPay, currencyCode),
      },
    ],
  };
}
