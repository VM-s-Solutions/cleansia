import {
  composeFinalPriceForUnquotedDiscount,
  composeSlotMoment,
  filterTimeOptionsForToday,
  generateTimeOptions,
} from './order-wizard.models';

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

describe('quarter-hour booking slots', () => {
  afterEach(() => jest.useRealTimers());

  it('applies the two-hour lead time and four-hour express boundary to quarter hours', () => {
    const now = new Date(2026, 8, 10, 10, 15);
    jest.useFakeTimers().setSystemTime(now);

    const options = filterTimeOptionsForToday(generateTimeOptions(), now);
    const availability = (value: string) =>
      options.find((option) => option.value === value)?.availability;

    expect(availability('12:00')).toBe('unavailable');
    expect(availability('12:15')).toBe('express');
    expect(availability('14:00')).toBe('express');
    expect(availability('14:15')).toBe('available');
  });

  it('does not round a slot into the minimum lead time', () => {
    const now = new Date(2026, 8, 10, 10, 15, 1);
    jest.useFakeTimers().setSystemTime(now);

    const options = filterTimeOptionsForToday(generateTimeOptions(), now);

    expect(
      options.find((option) => option.value === '12:15')?.availability
    ).toBe('unavailable');
    expect(
      options.find((option) => option.value === '12:30')?.availability
    ).toBe('express');
  });

  it.each(['10:15', '10:45'])(
    'preserves the selected local time %s in the submitted instant',
    (time) => {
      const day = new Date(2026, 8, 11);
      const instant = composeSlotMoment(day, time);

      expect(instant?.getDate()).toBe(11);
      expect(instant?.getHours()).toBe(10);
      expect(instant?.getMinutes()).toBe(Number(time.split(':')[1]));
      expect(instant?.getSeconds()).toBe(0);
    }
  );
});
