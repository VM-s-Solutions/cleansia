import { AdminReferralListItem } from '@cleansia/admin-services';
import { formatMoney } from '@cleansia/utils';
import { TranslateService } from '@ngx-translate/core';
import { formatReferralCredit, getReferralTableColumns } from './referrals-list.models';

function translateStub(lang: string): TranslateService {
  return {
    currentLang: lang,
    instant: (key: string, params?: Record<string, unknown>) =>
      key === 'pages.loyalty_referrals.credit_format' ? `${params?.['referrer']} / ${params?.['referred']}` : key,
  } as unknown as TranslateService;
}

const row = (fields: Partial<AdminReferralListItem>) => AdminReferralListItem.fromJS(fields);
const money = (value: number, code: string, locale: string) => formatMoney(value, code, locale, { fractionDigits: 2 });

describe('formatReferralCredit', () => {
  it('prints both grants in the currency they were paid in, for the language being read', () => {
    const paid = row({ creditAwardedToReferrer: 150, creditAwardedToReferred: 150, creditCurrencyCode: 'CZK' });

    expect(formatReferralCredit(paid, translateStub('cs'))).toBe(
      `${money(150, 'CZK', 'cs-CZ')} / ${money(150, 'CZK', 'cs-CZ')}`,
    );
    expect(formatReferralCredit(paid, translateStub('cs'))).toMatch(/^150,00\sKč \/ 150,00\sKč$/);
    expect(formatReferralCredit(paid, translateStub('en'))).toBe(
      `${money(150, 'CZK', 'en-US')} / ${money(150, 'CZK', 'en-US')}`,
    );
  });

  it('prints a side that received nothing as zero beside the side that was paid', () => {
    const oneSided = row({ creditAwardedToReferrer: 7.5, creditCurrencyCode: 'EUR' });

    expect(formatReferralCredit(oneSided, translateStub('en'))).toBe('€7.50 / €0.00');
  });

  it('says not yet when neither side has been credited', () => {
    expect(formatReferralCredit(row({}), translateStub('en'))).toBe('pages.loyalty_referrals.not_yet');
  });

  it('is the credit column the referrals table renders', () => {
    const paid = row({ creditAwardedToReferrer: 150, creditAwardedToReferred: 150, creditCurrencyCode: 'CZK' });
    const column = getReferralTableColumns(translateStub('en')).find((c) => c.id === 'creditAwarded');

    expect(column?.header).toBe('pages.loyalty_referrals.column.credit_awarded');
    expect(column?.numeric).toBe(true);
    expect(column?.getValue?.(paid)).toBe(`${money(150, 'CZK', 'en-US')} / ${money(150, 'CZK', 'en-US')}`);
  });
});
