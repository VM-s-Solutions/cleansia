import { TableColumn } from '@cleansia/components';
import { OrderEmployeePayDto } from '@cleansia/partner-services';
import { formatMoney, localeFor } from '@cleansia/utils';

/**
 * An invoice amount in the invoice's own currency, the way the session's language writes money.
 * An absent code renders the bare number rather than a guessed symbol: no symbol is visibly
 * incomplete, a wrong one is not.
 */
export function formatInvoiceAmount(
  value: number | undefined,
  currencyCode: string | undefined,
  lang: string | undefined
): string {
  if (value === undefined || value === null) return '';
  return formatMoney(value, currencyCode || undefined, localeFor(lang), { fractionDigits: 2 });
}

/** Every row is in the invoice's currency (one invoice per currency). */
export function getOrderPaysTableDefinition(
  currencyCode: string | undefined,
  lang: string | undefined
): { columns: TableColumn<OrderEmployeePayDto>[] } {
  const amount = (value: number | undefined): string => formatInvoiceAmount(value, currencyCode, lang);
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
