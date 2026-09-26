import { cashIsRefused, resolveCashEligibility } from './cash-eligibility.models';

describe('resolveCashEligibility — BookingPolicy.AllowsCash on the client', () => {
  it('allows cash to a signed-in customer on a booking one cleaner does alone', () => {
    expect(resolveCashEligibility(true, 1)).toEqual({ kind: 'available' });
  });

  it('refuses cash to a guest, and asks them to sign in', () => {
    expect(resolveCashEligibility(false, 1)).toEqual({ kind: 'needs_account' });
  });

  it('refuses cash to a signed-in customer when the server says two cleaners are needed', () => {
    expect(resolveCashEligibility(true, 2)).toEqual({ kind: 'needs_card', requiredCleaners: 2 });
  });

  it('names the crew rather than sign-in to a guest whose booking needs two cleaners', () => {
    expect(resolveCashEligibility(false, 3)).toEqual({ kind: 'needs_card', requiredCleaners: 3 });
  });

  it('refuses a guest before any quote has said how many cleaners are needed', () => {
    expect(resolveCashEligibility(false, null)).toEqual({ kind: 'needs_account' });
  });

  it('decides nothing for a signed-in customer until a quote describes the selection', () => {
    expect(resolveCashEligibility(true, null)).toEqual({ kind: 'pending' });
  });

  it('never allows cash on a crew figure the server does not produce', () => {
    expect(resolveCashEligibility(true, 0).kind).not.toBe('available');
  });

  it('refuses only on a verdict, never while the crew is unknown', () => {
    expect(cashIsRefused({ kind: 'needs_account' })).toBe(true);
    expect(cashIsRefused({ kind: 'needs_card', requiredCleaners: 2 })).toBe(true);
    expect(cashIsRefused({ kind: 'pending' })).toBe(false);
    expect(cashIsRefused({ kind: 'available' })).toBe(false);
  });
});
