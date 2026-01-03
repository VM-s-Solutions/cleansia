import { OrderEmployeePayDto } from '@cleansia/admin-services';
import { TableDefinition } from '@cleansia/components';
import { TranslateService } from '@ngx-translate/core';

export function getOrderPaysTableDefinition(
  translate: TranslateService
): TableDefinition<OrderEmployeePayDto> {
  return {
    columns: [
      {
        id: 'orderNumber',
        headerName: translate.instant(
          'pages.invoice_detail.order_pays.order_number'
        ),
        value: 'orderNumber',
        columnClass: 'width-12',
      },
      {
        id: 'basePay',
        headerName: translate.instant(
          'pages.invoice_detail.order_pays.base_pay'
        ),
        value: (row?: OrderEmployeePayDto) => {
          if (!row) return '';
          return `${row.basePay?.toFixed(2) || '0.00'}`;
        },
        columnClass: 'width-10',
      },
      {
        id: 'extrasPay',
        headerName: translate.instant(
          'pages.invoice_detail.order_pays.extras_pay'
        ),
        value: (row?: OrderEmployeePayDto) => {
          if (!row) return '';
          return `${row.extrasPay?.toFixed(2) || '0.00'}`;
        },
        columnClass: 'width-10',
      },
      {
        id: 'expensesPay',
        headerName: translate.instant(
          'pages.invoice_detail.order_pays.expenses_pay'
        ),
        value: (row?: OrderEmployeePayDto) => {
          if (!row) return '';
          return `${row.expensesPay?.toFixed(2) || '0.00'}`;
        },
        columnClass: 'width-10',
      },
      {
        id: 'bonusPay',
        headerName: translate.instant(
          'pages.invoice_detail.order_pays.bonus_pay'
        ),
        value: (row?: OrderEmployeePayDto) => {
          if (!row) return '';
          return `${row.bonusPay?.toFixed(2) || '0.00'}`;
        },
        columnClass: 'width-10',
      },
      {
        id: 'deductionPay',
        headerName: translate.instant(
          'pages.invoice_detail.order_pays.deduction_pay'
        ),
        value: (row?: OrderEmployeePayDto) => {
          if (!row) return '';
          return `${row.deductionPay?.toFixed(2) || '0.00'}`;
        },
        columnClass: 'width-10',
      },
      {
        id: 'totalPay',
        headerName: translate.instant(
          'pages.invoice_detail.order_pays.total_pay'
        ),
        value: (row?: OrderEmployeePayDto) => {
          if (!row) return '';
          return `${row.totalPay?.toFixed(2) || '0.00'}`;
        },
        columnClass: 'width-10',
      },
      {
        id: 'createdOn',
        headerName: translate.instant(
          'pages.invoice_detail.order_pays.created_on'
        ),
        value: (row?: OrderEmployeePayDto) => {
          if (!row?.createdOn) return '';
          const date =
            row.createdOn instanceof Date
              ? row.createdOn
              : new Date(row.createdOn);
          return date.toLocaleDateString('cs-CZ');
        },
        columnClass: 'width-10',
      },
    ],
  };
}
