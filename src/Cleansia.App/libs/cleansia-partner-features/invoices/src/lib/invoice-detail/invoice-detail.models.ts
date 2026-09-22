import { TableColumn } from '@cleansia/components';
import { OrderEmployeePayDto } from '@cleansia/partner-services';

/**
 * Every row is in the invoice's currency (one invoice per currency). An absent code renders the
 * amount with no symbol rather than a guessed one: no symbol is visibly incomplete, a wrong one is not.
 */
export function getOrderPaysTableDefinition(
  currencyCode: string | undefined
): { columns: TableColumn<OrderEmployeePayDto>[] } {
  const amount = (value: number | undefined): string =>
    `${value?.toFixed(2)} ${currencyCode ?? ''}`.trimEnd();
  return {
    columns: [
      {
        id: 'orderNumber',
        field: 'orderNumber',
        header: 'pages.invoice_detail.order_number',
        sortable: false,
      },
      {
        id: 'basePay',
        field: 'basePay',
        header: 'pages.invoice_detail.base_pay',
        sortable: false,
        align: 'right',
        getValue: (pay?: OrderEmployeePayDto) =>
          pay ? amount(pay.basePay) : '',
      },
      {
        id: 'extrasPay',
        field: 'extrasPay',
        header: 'pages.invoice_detail.extras_pay',
        sortable: false,
        align: 'right',
        getValue: (pay?: OrderEmployeePayDto) =>
          pay ? amount(pay.extrasPay) : '',
      },
      {
        id: 'expensesPay',
        field: 'expensesPay',
        header: 'pages.invoice_detail.expenses_pay',
        sortable: false,
        align: 'right',
        getValue: (pay?: OrderEmployeePayDto) =>
          pay ? amount(pay.expensesPay) : '',
      },
      {
        id: 'bonusPay',
        field: 'bonusPay',
        header: 'pages.invoice_detail.bonus_pay',
        sortable: false,
        align: 'right',
        getValue: (pay?: OrderEmployeePayDto) =>
          pay ? amount(pay.bonusPay) : '',
      },
      {
        id: 'deductionPay',
        field: 'deductionPay',
        header: 'pages.invoice_detail.deduction_pay',
        sortable: false,
        align: 'right',
        getValue: (pay?: OrderEmployeePayDto) =>
          pay ? amount(pay.deductionPay) : '',
      },
      {
        id: 'totalPay',
        field: 'totalPay',
        header: 'pages.invoice_detail.total_pay',
        sortable: false,
        align: 'right',
        getValue: (pay?: OrderEmployeePayDto) =>
          pay ? amount(pay.totalPay) : '',
      },
    ],
  };
}
