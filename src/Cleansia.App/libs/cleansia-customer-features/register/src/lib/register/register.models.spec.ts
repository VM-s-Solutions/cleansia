import { referralHelperCopy, referralSuccessCopy } from './register.models';

describe('the referral dialog copy', () => {
  it('states the market credit in the helper line when the market pays one', () => {
    expect(referralHelperCopy('150 Kč')).toEqual({
      key: 'auth.register.referral.dialog_helper',
      params: { amount: '150 Kč' },
    });
  });

  it('names no amount in the helper line when the market pays none', () => {
    expect(referralHelperCopy(null)).toEqual({
      key: 'auth.register.referral.dialog_helper_no_amount',
      params: {},
    });
  });

  it('confirms an accepted code with the credit, naming the referrer when known', () => {
    expect(referralSuccessCopy('Petra', '150 Kč')).toEqual({
      key: 'auth.register.referral.dialog_success_named',
      params: { name: 'Petra', amount: '150 Kč' },
    });
    expect(referralSuccessCopy(null, '150 Kč')).toEqual({
      key: 'auth.register.referral.dialog_success',
      params: { amount: '150 Kč' },
    });
  });

  it('confirms an accepted code without an amount when the market pays none', () => {
    expect(referralSuccessCopy('Petra', null)).toEqual({
      key: 'auth.register.referral.dialog_success_named_no_amount',
      params: { name: 'Petra' },
    });
    expect(referralSuccessCopy('  ', null)).toEqual({
      key: 'auth.register.referral.dialog_success_no_amount',
      params: {},
    });
  });
});
