import { OrderEmployeePayDto, PayLineType } from '@cleansia/admin-services';
import { TableColumn, TableAction } from '@cleansia/components';
import { formatDate, formatMoney, localeFor } from '@cleansia/utils';
import { TranslateService } from '@ngx-translate/core';

export const PAY_LINE_TYPE_LABEL_KEYS: Readonly<Record<PayLineType, string>> = {
  [PayLineType.Job]: 'enums.pay_line_type.job',
  [PayLineType.CancellationFeeShare]: 'enums.pay_line_type.cancellation_fee_share',
  [PayLineType.LockoutFeeShare]: 'enums.pay_line_type.lockout_fee_share',
};

export function getOrderPaysTableDefinition(
  translate: TranslateService
): { columns: TableColumn<OrderEmployeePayDto>[]; actions: TableAction<OrderEmployeePayDto>[] } {
  const pay = (row: OrderEmployeePayDto, amount: number | undefined): string =>
    formatMoney(amount ?? 0, row.currencyCode, localeFor(translate.currentLang), { fractionDigits: 2 });
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
        id: 'lineType',
        field: 'lineType',
        header: translate.instant(
          'pages.invoice_detail.order_pays.line_type'
        ),
        getValue: (row: OrderEmployeePayDto) => {
          const key = PAY_LINE_TYPE_LABEL_KEYS[row.lineType];
          return key ? translate.instant(key) : '-';
        },
        width: '12%',
      },
      {
        id: 'basePay',
        field: 'basePay',
        header: translate.instant(
          'pages.invoice_detail.order_pays.base_pay'
        ),
        getValue: (row: OrderEmployeePayDto) => pay(row, row.basePay),
        numeric: true,
        width: '10%',
      },
      {
        id: 'extrasPay',
        field: 'extrasPay',
        header: translate.instant(
          'pages.invoice_detail.order_pays.extras_pay'
        ),
        getValue: (row: OrderEmployeePayDto) => pay(row, row.extrasPay),
        numeric: true,
        width: '10%',
      },
      {
        id: 'expensesPay',
        field: 'expensesPay',
        header: translate.instant(
          'pages.invoice_detail.order_pays.expenses_pay'
        ),
        getValue: (row: OrderEmployeePayDto) => pay(row, row.expensesPay),
        numeric: true,
        width: '10%',
      },
      {
        id: 'bonusPay',
        field: 'bonusPay',
        header: translate.instant(
          'pages.invoice_detail.order_pays.bonus_pay'
        ),
        getValue: (row: OrderEmployeePayDto) => pay(row, row.bonusPay),
        numeric: true,
        width: '10%',
      },
      {
        id: 'deductionPay',
        field: 'deductionPay',
        header: translate.instant(
          'pages.invoice_detail.order_pays.deduction_pay'
        ),
        getValue: (row: OrderEmployeePayDto) => pay(row, row.deductionPay),
        numeric: true,
        width: '10%',
      },
      {
        id: 'totalPay',
        field: 'totalPay',
        header: translate.instant(
          'pages.invoice_detail.order_pays.total_pay'
        ),
        getValue: (row: OrderEmployeePayDto) => pay(row, row.totalPay),
        numeric: true,
        width: '10%',
      },
      {
        id: 'createdOn',
        field: 'createdOn',
        header: translate.instant(
          'pages.invoice_detail.order_pays.created_on'
        ),
        getValue: (row: OrderEmployeePayDto) => formatDate(row.createdOn, translate.currentLang),
        numeric: true,
        width: '10%',
      },
    ],
    actions: [],
  };
}
