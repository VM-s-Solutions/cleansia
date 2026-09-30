import { OrderEmployeePayDto, PayLineType } from '@cleansia/admin-services';
import { TranslateService } from '@ngx-translate/core';
import { getOrderPaysTableDefinition } from './invoice-detail.models';

describe('invoice detail pay lines', () => {
  const translate = { instant: (key: string) => key, currentLang: 'en' } as unknown as TranslateService;
  const lineOf = (lineType: PayLineType) =>
    getOrderPaysTableDefinition(translate)
      .columns.find((column) => column.id === 'lineType')
      ?.getValue?.(OrderEmployeePayDto.fromJS({ orderNumber: 'CL-1001', lineType }));

  it('names a job line and each share of a collected fee as its own kind of line', () => {
    expect(lineOf(PayLineType.Job)).toBe('enums.pay_line_type.job');
    expect(lineOf(PayLineType.CancellationFeeShare)).toBe('enums.pay_line_type.cancellation_fee_share');
    expect(lineOf(PayLineType.LockoutFeeShare)).toBe('enums.pay_line_type.lockout_fee_share');
  });

  it('prints a dash for a line type the screen does not know yet rather than a machine key', () => {
    expect(lineOf(99 as PayLineType)).toBe('-');
  });
});
