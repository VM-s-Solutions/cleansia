import { OrderEmployeePayDto } from '@cleansia/admin-services';
import { TableColumn, TableAction } from '@cleansia/components';
import { TranslateService } from '@ngx-translate/core';

export function getOrderPaysTableDefinition(
  translate: TranslateService
): { columns: TableColumn<OrderEmployeePayDto>[]; actions: TableAction<OrderEmployeePayDto>[] } {
  return {
    columns: [
      {
        id: 'orderNumber',
        field: 'orderNumber',
        header: translate.instant(
          'pages.invoice_detail.order_pays.order_number'
        ),
        width: '12%',
      },
      {
        id: 'basePay',
        field: 'basePay',
        header: translate.instant(
          'pages.invoice_detail.order_pays.base_pay'
        ),
        getValue: (row: OrderEmployeePayDto) => {
          return `${row.basePay?.toFixed(2) || '0.00'}`;
        },
        width: '10%',
      },
      {
        id: 'extrasPay',
        field: 'extrasPay',
        header: translate.instant(
          'pages.invoice_detail.order_pays.extras_pay'
        ),
        getValue: (row: OrderEmployeePayDto) => {
          return `${row.extrasPay?.toFixed(2) || '0.00'}`;
        },
        width: '10%',
      },
      {
        id: 'expensesPay',
        field: 'expensesPay',
        header: translate.instant(
          'pages.invoice_detail.order_pays.expenses_pay'
        ),
        getValue: (row: OrderEmployeePayDto) => {
          return `${row.expensesPay?.toFixed(2) || '0.00'}`;
        },
        width: '10%',
      },
      {
        id: 'bonusPay',
        field: 'bonusPay',
        header: translate.instant(
          'pages.invoice_detail.order_pays.bonus_pay'
        ),
        getValue: (row: OrderEmployeePayDto) => {
          return `${row.bonusPay?.toFixed(2) || '0.00'}`;
        },
        width: '10%',
      },
      {
        id: 'deductionPay',
        field: 'deductionPay',
        header: translate.instant(
          'pages.invoice_detail.order_pays.deduction_pay'
        ),
        getValue: (row: OrderEmployeePayDto) => {
          return `${row.deductionPay?.toFixed(2) || '0.00'}`;
        },
        width: '10%',
      },
      {
        id: 'totalPay',
        field: 'totalPay',
        header: translate.instant(
          'pages.invoice_detail.order_pays.total_pay'
        ),
        getValue: (row: OrderEmployeePayDto) => {
          return `${row.totalPay?.toFixed(2) || '0.00'}`;
        },
        width: '10%',
      },
      {
        id: 'createdOn',
        field: 'createdOn',
        header: translate.instant(
          'pages.invoice_detail.order_pays.created_on'
        ),
        getValue: (row: OrderEmployeePayDto) => {
          if (!row.createdOn) return '';
          const date =
            row.createdOn instanceof Date
              ? row.createdOn
              : new Date(row.createdOn);
          return date.toLocaleDateString('en-GB');
        },
        width: '10%',
      },
    ],
    actions: [],
  };
}
