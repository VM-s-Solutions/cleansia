import { AdminReferralListItem, ReferralStatus } from '@cleansia/admin-services';
import { formatMoney } from '@cleansia/utils';
import { TranslateService } from '@ngx-translate/core';
import {
  formatHoldReasons,
  formatReferralCredit,
  getReferralInterventionActions,
  getReferralTableColumns,
  isHeldReferral,
} from './referrals-list.models';

function translateStub(lang: string): TranslateService {
  return {
    currentLang: lang,
    instant: (key: string, params?: Record<string, unknown>) => {
      if (key === 'pages.loyalty_referrals.credit_format') return `${params?.['referrer']} / ${params?.['referred']}`;
      if (key === 'pages.loyalty_referrals.not_yet') return '—';
      return key;
    },
  } as unknown as TranslateService;
}

const row = (fields: Partial<AdminReferralListItem>) => AdminReferralListItem.fromJS(fields);
const money = (value: number, code: string, locale: string) => formatMoney(value, code, locale, { fractionDigits: 2 });

describe('formatReferralCredit', () => {
  it('prints both grants in the currency they were paid in, for the language being read', () => {
    const paid = row({
      creditAwardedToReferrer: 150,
      referrerCreditCurrencyCode: 'CZK',
      creditAwardedToReferred: 150,
      referredCreditCurrencyCode: 'CZK',
    });

    expect(formatReferralCredit(paid, translateStub('cs'))).toBe(
      `${money(150, 'CZK', 'cs-CZ')} / ${money(150, 'CZK', 'cs-CZ')}`,
    );
    expect(formatReferralCredit(paid, translateStub('cs'))).toMatch(/^150,00\sKč \/ 150,00\sKč$/);
    expect(formatReferralCredit(paid, translateStub('en'))).toBe(
      `${money(150, 'CZK', 'en-US')} / ${money(150, 'CZK', 'en-US')}`,
    );
  });

  it('prints a dash for the side that was paid nothing, which has no currency, beside the side that was paid', () => {
    const oneSided = row({ creditAwardedToReferrer: 7.5, referrerCreditCurrencyCode: 'EUR' });

    expect(formatReferralCredit(oneSided, translateStub('en'))).toBe('€7.50 / —');
  });

  it('prints each side in its own currency when the two were paid in different ones', () => {
    const crossMarket = row({
      creditAwardedToReferrer: 150,
      referrerCreditCurrencyCode: 'CZK',
      creditAwardedToReferred: 6,
      referredCreditCurrencyCode: 'EUR',
    });

    expect(formatReferralCredit(crossMarket, translateStub('en'))).toBe(
      `${money(150, 'CZK', 'en-US')} / ${money(6, 'EUR', 'en-US')}`,
    );
  });

  it('says not yet when neither side has been credited', () => {
    expect(formatReferralCredit(row({}), translateStub('en'))).toBe('—');
  });

  it('is the credit column the referrals table renders', () => {
    const paid = row({
      creditAwardedToReferrer: 150,
      referrerCreditCurrencyCode: 'CZK',
      creditAwardedToReferred: 150,
      referredCreditCurrencyCode: 'CZK',
    });
    const column = getReferralTableColumns(translateStub('en')).find((c) => c.id === 'creditAwarded');

    expect(column?.header).toBe('pages.loyalty_referrals.column.credit_awarded');
    expect(column?.numeric).toBe(true);
    expect(column?.getValue?.(paid)).toBe(`${money(150, 'CZK', 'en-US')} / ${money(150, 'CZK', 'en-US')}`);
  });
});

describe('a held referral', () => {
  const held = row({ status: ReferralStatus.Accepted, holdReasons: 'address,phone,email' });

  it('is an accepted referral that carries hold reasons', () => {
    expect(isHeldReferral(held)).toBe(true);
    expect(isHeldReferral(row({ status: ReferralStatus.Accepted }))).toBe(false);
  });

  it('is no longer held once released or rejected, though its reasons stay on the row', () => {
    expect(isHeldReferral(row({ status: ReferralStatus.Qualified, holdReasons: 'address' }))).toBe(false);
    expect(isHeldReferral(row({ status: ReferralStatus.Reversed, holdReasons: 'address' }))).toBe(false);
  });

  it('names every reason it was held for, each through its own label', () => {
    expect(formatHoldReasons(held, translateStub('en'))).toBe(
      'pages.loyalty_referrals.hold_reason.address, pages.loyalty_referrals.hold_reason.phone, pages.loyalty_referrals.hold_reason.email',
    );
    expect(formatHoldReasons(row({ status: ReferralStatus.Accepted, holdReasons: 'phone' }), translateStub('en'))).toBe(
      'pages.loyalty_referrals.hold_reason.phone',
    );
  });

  it('names nothing for a referral that is not held', () => {
    expect(formatHoldReasons(row({ status: ReferralStatus.Accepted }), translateStub('en'))).toBeNull();
    expect(formatHoldReasons(row({ status: ReferralStatus.Qualified, holdReasons: 'address' }), translateStub('en'))).toBeNull();
  });
});

describe('getReferralInterventionActions', () => {
  const handlers = {
    onReverse: jest.fn(),
    onForceQualify: jest.fn(),
    onRelease: jest.fn(),
    onReject: jest.fn(),
  };

  const tooltipsShownFor = (referral: AdminReferralListItem): string[] =>
    getReferralInterventionActions({ canIntervene: true, ...handlers }, translateStub('en'))
      .filter((action) => action.visible?.(referral) ?? true)
      .map((action) => action.tooltip ?? '');

  beforeEach(() => jest.clearAllMocks());

  it('offers a held referral release and reject, and nothing else', () => {
    expect(tooltipsShownFor(row({ status: ReferralStatus.Accepted, holdReasons: 'address' }))).toEqual([
      'pages.loyalty_referrals.actions.release',
      'pages.loyalty_referrals.actions.reject',
    ]);
  });

  it('offers an accepted referral that is not held only force-qualify', () => {
    expect(tooltipsShownFor(row({ status: ReferralStatus.Accepted }))).toEqual([
      'pages.loyalty_referrals.actions.force_qualify',
    ]);
  });

  it('offers a qualified referral only reverse, even one that was held before it was released', () => {
    expect(tooltipsShownFor(row({ status: ReferralStatus.Qualified }))).toEqual(['pages.loyalty_referrals.actions.reverse']);
    expect(tooltipsShownFor(row({ status: ReferralStatus.Qualified, holdReasons: 'phone' }))).toEqual([
      'pages.loyalty_referrals.actions.reverse',
    ]);
  });

  it('offers an expired or reversed referral nothing', () => {
    expect(tooltipsShownFor(row({ status: ReferralStatus.Expired }))).toEqual([]);
    expect(tooltipsShownFor(row({ status: ReferralStatus.Reversed, holdReasons: 'address' }))).toEqual([]);
  });

  it('sends release and reject to their own handlers', () => {
    const held = row({ id: 'ref-9', status: ReferralStatus.Accepted, holdReasons: 'email' });
    const actions = getReferralInterventionActions({ canIntervene: true, ...handlers }, translateStub('en'));

    actions.find((a) => a.tooltip === 'pages.loyalty_referrals.actions.release')?.onClick(held);
    actions.find((a) => a.tooltip === 'pages.loyalty_referrals.actions.reject')?.onClick(held);

    expect(handlers.onRelease).toHaveBeenCalledWith(held);
    expect(handlers.onReject).toHaveBeenCalledWith(held);
    expect(handlers.onForceQualify).not.toHaveBeenCalled();
    expect(handlers.onReverse).not.toHaveBeenCalled();
  });

  it('offers nothing to an administrator who may not intervene', () => {
    expect(getReferralInterventionActions({ canIntervene: false, ...handlers }, translateStub('en'))).toEqual([]);
  });
});
