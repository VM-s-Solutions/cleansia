import { OrderEmployeePayDto } from '@cleansia/partner-services';
import { formatInvoiceAmount, getOrderPaysTableDefinition } from './invoice-detail.models';

describe('getOrderPaysTableDefinition', () => {
  const totalOf = (code: string | undefined, lang: string, row: OrderEmployeePayDto): unknown => {
    const column = getOrderPaysTableDefinition(code, lang).columns.find((c) => c.id === 'totalPay');
    if (!column?.getValue) throw new Error('totalPay column missing or static');
    return column.getValue(row);
  };

  it('labels every row with the invoice currency in the session language', () => {
    expect(totalOf('EUR', 'en', OrderEmployeePayDto.fromJS({ totalPay: 99 }))).toBe('€99.00');
    expect(totalOf('CZK', 'cs', OrderEmployeePayDto.fromJS({ totalPay: 1250 }))).toBe('1 250,00 Kč');
  });

  // The invoice always names its currency; a missing code is visibly incomplete, a guessed one is not.
  it('prints a bare number rather than a currency the invoice does not name', () => {
    expect(totalOf(undefined, 'en', OrderEmployeePayDto.fromJS({ totalPay: 99 }))).toBe('99.00');
  });

  it('right-aligns every amount column in tabular figures', () => {
    const { columns } = getOrderPaysTableDefinition('CZK', 'cs');
    for (const column of columns.filter((c) => c.id !== 'orderNumber')) {
      expect(column.numeric).toBe(true);
    }
  });
});

describe('formatInvoiceAmount', () => {
  it('prints nothing for a missing amount', () => {
    expect(formatInvoiceAmount(undefined, 'CZK', 'cs')).toBe('');
  });
});
