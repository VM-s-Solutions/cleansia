import { TranslateService } from '@ngx-translate/core';
import { formatPayPeriodDuration } from './pay-period-management.models';

describe('pay period duration', () => {
  const translateIn = (currentLang: string) =>
    ({
      currentLang,
      instant: (key: string, params?: { count?: number }) =>
        `${key}:${params?.count}`,
    } as unknown as TranslateService);

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
