import { composeFinalPriceForUnquotedDiscount } from './order-wizard.models';

describe('composeFinalPriceForUnquotedDiscount', () => {
  it('subtracts the discount when no surcharge is in the gross', () => {
    expect(composeFinalPriceForUnquotedDiscount(1000, 1000, 200)).toBe(800);
  });

  it('surcharges the discounted subtotal, not the discounted gross', () => {
    // The server: (1000 - 200) * 1.2. Discounting the gross would read 1200 - 200 = 1000.
    expect(composeFinalPriceForUnquotedDiscount(1000, 1200, 200)).toBe(960);
  });

  it('returns the gross unchanged for a zero discount', () => {
    expect(composeFinalPriceForUnquotedDiscount(1000, 1200, 0)).toBe(1200);
  });

  it('floors at zero when the discount covers the whole subtotal', () => {
    expect(composeFinalPriceForUnquotedDiscount(1000, 1200, 1000)).toBe(0);
    expect(composeFinalPriceForUnquotedDiscount(1000, 1200, 5000)).toBe(0);
  });

  it('is zero for an empty basket rather than dividing by it', () => {
    expect(composeFinalPriceForUnquotedDiscount(0, 0, 100)).toBe(0);
  });

  it('rounds to whole cents instead of leaking binary dust', () => {
    expect(composeFinalPriceForUnquotedDiscount(100, 120, 0.01)).toBe(119.99);
  });
});
