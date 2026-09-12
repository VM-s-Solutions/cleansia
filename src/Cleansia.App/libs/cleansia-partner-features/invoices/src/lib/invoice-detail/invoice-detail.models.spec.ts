import { OrderEmployeePayDto } from '@cleansia/partner-services';
import { getOrderPaysTableDefinition } from './invoice-detail.models';

describe('getOrderPaysTableDefinition', () => {
  const total = (code: string | undefined) =>
    getOrderPaysTableDefinition(code).columns.find((c) => c.id === 'totalPay')!;

  it('labels every row with the invoice currency', () => {
    expect(total('EUR').getValue!(OrderEmployeePayDto.fromJS({ totalPay: 99 }))).toBe('99.00 EUR');
  });

  // The invoice always names its currency; a missing code is visibly incomplete, a guessed one is not.
  it('prints a bare number rather than a currency the invoice does not name', () => {
    expect(total(undefined).getValue!(OrderEmployeePayDto.fromJS({ totalPay: 99 }))).toBe('99.00');
  });
});
