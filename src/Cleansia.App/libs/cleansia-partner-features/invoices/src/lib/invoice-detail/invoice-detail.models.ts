import { TableColumn } from '@cleansia/components';
import { OrderEmployeePayDto } from '@cleansia/partner-services';

export function getOrderPaysTableDefinition(
  currencyCode: string
): { columns: TableColumn<OrderEmployeePayDto>[] } {
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
          pay ? `${pay.basePay?.toFixed(2)} ${currencyCode}` : '',
      },
      {
        id: 'extrasPay',
        field: 'extrasPay',
        header: 'pages.invoice_detail.extras_pay',
        sortable: false,
        align: 'right',
        getValue: (pay?: OrderEmployeePayDto) =>
          pay ? `${pay.extrasPay?.toFixed(2)} ${currencyCode}` : '',
      },
      {
        id: 'expensesPay',
        field: 'expensesPay',
        header: 'pages.invoice_detail.expenses_pay',
        sortable: false,
        align: 'right',
        getValue: (pay?: OrderEmployeePayDto) =>
          pay ? `${pay.expensesPay?.toFixed(2)} ${currencyCode}` : '',
      },
      {
        id: 'bonusPay',
        field: 'bonusPay',
        header: 'pages.invoice_detail.bonus_pay',
        sortable: false,
        align: 'right',
        getValue: (pay?: OrderEmployeePayDto) =>
          pay ? `${pay.bonusPay?.toFixed(2)} ${currencyCode}` : '',
      },
      {
        id: 'deductionPay',
        field: 'deductionPay',
        header: 'pages.invoice_detail.deduction_pay',
        sortable: false,
        align: 'right',
        getValue: (pay?: OrderEmployeePayDto) =>
          pay ? `${pay.deductionPay?.toFixed(2)} ${currencyCode}` : '',
      },
      {
        id: 'totalPay',
        field: 'totalPay',
        header: 'pages.invoice_detail.total_pay',
        sortable: false,
        align: 'right',
        getValue: (pay?: OrderEmployeePayDto) =>
          pay ? `${pay.totalPay?.toFixed(2)} ${currencyCode}` : '',
      },
    ],
  };
}
