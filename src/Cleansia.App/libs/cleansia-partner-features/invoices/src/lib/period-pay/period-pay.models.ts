import { TableColumn } from '@cleansia/components';
import { OrderEmployeePayDto } from '@cleansia/partner-services';

export type PeriodStatusKey = 'open' | 'closed' | 'paid' | 'unknown';

const CURRENCY_SUFFIX = 'Kč';

export function formatPayAmount(value: number | undefined): string {
  return value !== undefined && value !== null
    ? `${value.toFixed(2)} ${CURRENCY_SUFFIX}`
    : '';
}

export function getPeriodPayTableDefinition(): {
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
        getValue: (pay?: OrderEmployeePayDto) => formatPayAmount(pay?.basePay),
      },
      {
        id: 'extrasPay',
        field: 'extrasPay',
        header: 'pages.period_pay.extras_pay',
        sortable: false,
        align: 'right',
        getValue: (pay?: OrderEmployeePayDto) => formatPayAmount(pay?.extrasPay),
      },
      {
        id: 'expensesPay',
        field: 'expensesPay',
        header: 'pages.period_pay.expenses_pay',
        sortable: false,
        align: 'right',
        getValue: (pay?: OrderEmployeePayDto) => formatPayAmount(pay?.expensesPay),
      },
      {
        id: 'bonusPay',
        field: 'bonusPay',
        header: 'pages.period_pay.bonus_pay',
        sortable: false,
        align: 'right',
        getValue: (pay?: OrderEmployeePayDto) => formatPayAmount(pay?.bonusPay),
      },
      {
        id: 'deductionPay',
        field: 'deductionPay',
        header: 'pages.period_pay.deduction_pay',
        sortable: false,
        align: 'right',
        getValue: (pay?: OrderEmployeePayDto) => formatPayAmount(pay?.deductionPay),
      },
      {
        id: 'totalPay',
        field: 'totalPay',
        header: 'pages.period_pay.total_pay',
        sortable: false,
        align: 'right',
        getValue: (pay?: OrderEmployeePayDto) => formatPayAmount(pay?.totalPay),
      },
    ],
  };
}
