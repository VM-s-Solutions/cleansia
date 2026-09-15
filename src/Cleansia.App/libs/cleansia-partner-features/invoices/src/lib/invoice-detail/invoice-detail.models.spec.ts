import { OrderEmployeePayDto } from '@cleansia/partner-services';
import { getOrderPaysTableDefinition } from './invoice-detail.models';

describe('getOrderPaysTableDefinition', () => {
  const totalOf = (code: string | undefined, row: OrderEmployeePayDto): unknown => {
    const column = getOrderPaysTableDefinition(code).columns.find((c) => c.id === 'totalPay');
    if (!column?.getValue) throw new Error('totalPay column missing or static');
    return column.getValue(row);
  };

  it('labels every row with the invoice currency', () => {
    expect(totalOf('EUR', OrderEmployeePayDto.fromJS({ totalPay: 99 }))).toBe('99.00 EUR');
  });

  // The invoice always names its currency; a missing code is visibly incomplete, a guessed one is not.
  it('prints a bare number rather than a currency the invoice does not name', () => {
    expect(totalOf(undefined, OrderEmployeePayDto.fromJS({ totalPay: 99 }))).toBe('99.00');
  });
});
