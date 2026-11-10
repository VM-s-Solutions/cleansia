import { PayPeriodDto } from '@cleansia/admin-services';
import { TranslateService } from '@ngx-translate/core';
import {
  formatPayPeriodDuration,
  getPayPeriodTableColumns,
} from './pay-period-management.models';

const translateIn = (currentLang: string) =>
  ({
    currentLang,
    instant: (key: string, params?: { count?: number }) =>
      `${key}:${params?.count}`,
  } as unknown as TranslateService);

describe('pay period list columns', () => {
  const cs = translateIn('cs');

  it('drops the Period column, since Start and End sit beside it', () => {
    expect(getPayPeriodTableColumns(cs).map((c) => c.id)).toEqual([
      'startDate',
      'endDate',
      'durationDays',
      'status',
      'closedAt',
      'closedBy',
    ]);
  });

  it("prints the start, the end and the duration in the session's language", () => {
    const row = PayPeriodDto.fromJS({
      startDate: '2026-11-01',
      endDate: '2026-11-03',
      durationDays: 3,
    });
    const value = (id: string) =>
      getPayPeriodTableColumns(cs)
        .find((c) => c.id === id)
        ?.getValue?.(row);

    expect(value('startDate')).toBe('1. 11. 2026');
    expect(value('endDate')).toBe('3. 11. 2026');
    expect(value('durationDays')).toBe('pay_periods.days_few:3');
  });
});

describe('pay period closed by', () => {
  const closedBy = (value?: string) =>
    getPayPeriodTableColumns(translateIn('cs'))
      .find((c) => c.id === 'closedBy')
      ?.getValue?.(PayPeriodDto.fromJS({ closedBy: value }));

  it("reads an automatic close as 'Automatically' in the session's language", () => {
    expect(closedBy('System')).toBe('pay_periods.closed_by_system:undefined');
  });

  it("shows the admin's e-mail unchanged, and a dash when nobody closed it", () => {
    expect(closedBy('admin@cleansia.cz')).toBe('admin@cleansia.cz');
    expect(closedBy(undefined)).toBe('-');
  });
});

describe('pay period duration', () => {
  it('names the day count in the plural form its language needs', () => {
    const cs = translateIn('cs');

    expect(formatPayPeriodDuration(1, cs)).toBe('pay_periods.days_one:1');
    expect(formatPayPeriodDuration(3, cs)).toBe('pay_periods.days_few:3');
    expect(formatPayPeriodDuration(14, cs)).toBe('pay_periods.days_other:14');
    expect(formatPayPeriodDuration(14, translateIn('ru'))).toBe(
      'pay_periods.days_many:14'
    );
    expect(formatPayPeriodDuration(21, translateIn('uk'))).toBe(
      'pay_periods.days_one:21'
    );
    expect(formatPayPeriodDuration(14, translateIn('en'))).toBe(
      'pay_periods.days_other:14'
    );
  });
});
