import { cashIsRefused, hasUsableCashCard, resolveCashEligibility } from './cash-eligibility.models';

describe('resolveCashEligibility — BookingPolicy.AllowsCash on the client', () => {
  it('allows cash to a signed-in customer on a booking one cleaner does alone', () => {
    expect(resolveCashEligibility(true, 1, true)).toEqual({ kind: 'available' });
  });

  it('refuses cash to a guest, and asks them to sign in', () => {
    expect(resolveCashEligibility(false, 1, true)).toEqual({ kind: 'needs_account' });
  });

  it('refuses cash to a signed-in customer when the server says two cleaners are needed', () => {
    expect(resolveCashEligibility(true, 2, true)).toEqual({ kind: 'needs_card', requiredCleaners: 2 });
  });

  it('names the crew rather than sign-in to a guest whose booking needs two cleaners', () => {
    expect(resolveCashEligibility(false, 3, true)).toEqual({ kind: 'needs_card', requiredCleaners: 3 });
  });

  it('refuses a guest before any quote has said how many cleaners are needed', () => {
    expect(resolveCashEligibility(false, null, true)).toEqual({ kind: 'needs_account' });
  });

  it('decides nothing for a signed-in customer until a quote describes the selection', () => {
    expect(resolveCashEligibility(true, null, true)).toEqual({ kind: 'pending' });
  });

  it('never allows cash on a crew figure the server does not produce', () => {
    expect(resolveCashEligibility(true, 0, true).kind).not.toBe('available');
  });

  it('refuses only on a verdict, never while the crew is unknown', () => {
    expect(cashIsRefused({ kind: 'needs_saved_card' })).toBe(true);
    expect(cashIsRefused({ kind: 'needs_account' })).toBe(true);
    expect(cashIsRefused({ kind: 'needs_card', requiredCleaners: 2 })).toBe(true);
    expect(cashIsRefused({ kind: 'pending' })).toBe(false);
    expect(cashIsRefused({ kind: 'available' })).toBe(false);
  });
});


describe('saved cards for cash', () => {
  const card = { id: 'card', currencyCode: 'CZK', expMonth: 10, expYear: 2026 };
  const today = new Date('2026-10-31T23:59:59Z');

  it('requires a card, after the current read has succeeded', () => {
    expect(resolveCashEligibility(true, 1, false)).toEqual({ kind: 'needs_saved_card' });
    expect(resolveCashEligibility(true, 1, null)).toEqual({ kind: 'pending' });
    expect(hasUsableCashCard(null, 'CZK', today)).toBeNull();
    expect(hasUsableCashCard([], 'CZK', today)).toBe(false);
  });

  it('uses the quote currency and keeps the card through its whole UTC expiry month', () => {
    expect(hasUsableCashCard([card], 'czk', today)).toBe(true);
    expect(hasUsableCashCard([card], 'EUR', today)).toBe(false);
    expect(hasUsableCashCard([card], null, today)).toBeNull();
    expect(hasUsableCashCard([card], 'CZK', new Date('2026-11-01T00:00:00Z'))).toBe(false);
  });

  it('cannot treat a malformed or unidentified card as usable', () => {
    expect(hasUsableCashCard([{ ...card, expMonth: 13 }], 'CZK', today)).toBe(false);
    expect(hasUsableCashCard([{ ...card, id: undefined }], 'CZK', today)).toBe(false);
  });
});
