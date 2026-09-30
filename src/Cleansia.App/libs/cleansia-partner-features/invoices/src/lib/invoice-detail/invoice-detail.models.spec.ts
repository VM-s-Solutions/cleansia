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
    for (const column of columns.filter((c) => c.id !== 'orderNumber' && c.id !== 'deductionReason')) {
      expect(column.numeric).toBe(true);
    }
  });

  it('prints the reason given for a deduction beside it, and nothing for a row without one', () => {
    const { columns } = getOrderPaysTableDefinition('CZK', 'cs');
    const ids = columns.map((column) => column.id);
    expect(ids.indexOf('deductionReason')).toBe(ids.indexOf('deductionPay') + 1);

    const reason = columns.find((column) => column.id === 'deductionReason');
    if (!reason?.getValue) throw new Error('deductionReason column missing or static');
    expect(
      reason.getValue(OrderEmployeePayDto.fromJS({ deductionPay: 200, deductionReason: 'Kitchen left uncleaned' }))
    ).toBe('Kitchen left uncleaned');
    expect(reason.getValue(OrderEmployeePayDto.fromJS({ deductionPay: 0 }))).toBe('');
  });
});

describe('the dirtiness term on an invoice line', () => {
  it('sits beside the extras and prints in the invoice currency', () => {
    const { columns } = getOrderPaysTableDefinition('CZK', 'cs');
    const ids = columns.map((column) => column.id);
    expect(ids.indexOf('dirtinessPay')).toBe(ids.indexOf('extrasPay') + 1);

    const dirtiness = columns.find((column) => column.id === 'dirtinessPay');
    if (!dirtiness?.getValue) throw new Error('dirtinessPay column missing or static');
    expect(dirtiness.getValue(OrderEmployeePayDto.fromJS({ dirtinessPay: 195 }))).toBe('195,00 Kč');
  });
});

describe('formatInvoiceAmount', () => {
  it('prints nothing for a missing amount', () => {
    expect(formatInvoiceAmount(undefined, 'CZK', 'cs')).toBe('');
  });
});
