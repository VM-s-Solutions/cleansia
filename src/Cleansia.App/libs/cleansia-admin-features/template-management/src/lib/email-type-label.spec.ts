import { EmailType } from '@cleansia/admin-services';
import { TranslateService } from '@ngx-translate/core';
import { emailTypeLabel } from './email-type-label';

const translate = (dict: Record<string, string>) =>
  ({ instant: (k: string) => dict[k] ?? k }) as unknown as TranslateService;

describe('emailTypeLabel', () => {
  it('words the type from its key', () => {
    const t = translate({ 'enums.email_type.period_end_reminder': 'Připomenutí' });
    expect(emailTypeLabel(t, EmailType.PeriodEndReminder, 'Period End Reminder')).toBe('Připomenutí');
  });

  it('falls back to the server name when no key exists', () => {
    expect(emailTypeLabel(translate({}), EmailType.PromoCode, 'Promo Code')).toBe('Promo Code');
  });
});
