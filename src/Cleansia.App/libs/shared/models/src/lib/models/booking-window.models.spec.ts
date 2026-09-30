import {
  MAX_BOOKING_HORIZON_DAYS,
  generateTimeOptions,
  lastBookableDay,
} from './booking-window.models';

describe('booking time options', () => {
  it('offers every quarter hour within the daily booking window', () => {
    const options = generateTimeOptions();

    expect(options).toHaveLength(48);
    expect(options[0].value).toBe('08:00');
    expect(options.at(-1)?.value).toBe('19:45');
    expect(
      options
        .filter(({ value }) => value.startsWith('10:'))
        .map(({ value }) => value)
    ).toEqual(['10:00', '10:15', '10:30', '10:45']);
    expect(new Set(options.map(({ value }) => value)).size).toBe(
      options.length
    );
    expect(options.every(({ label, value }) => label === value)).toBe(true);
  });
});

describe('lastBookableDay', () => {
  const horizonOf = (now: Date) =>
    now.getTime() + MAX_BOOKING_HORIZON_DAYS * 24 * 60 * 60 * 1000;

  it('is the day before the horizon falls, at midnight', () => {
    expect(lastBookableDay(new Date(2026, 8, 29, 14, 0))).toEqual(
      new Date(2026, 10, 27)
    );
  });

  it.each([
    new Date(2026, 8, 29, 0, 0),
    new Date(2026, 8, 29, 7, 59),
    new Date(2026, 9, 24, 23, 59),
    new Date(2027, 1, 1, 12, 30),
  ])('offers no slot beyond the sixty-day horizon from %s', (now) => {
    const last = lastBookableDay(now);
    const latestSlot = new Date(
      last.getFullYear(),
      last.getMonth(),
      last.getDate(),
      19,
      45
    );

    expect(latestSlot.getTime()).toBeLessThanOrEqual(horizonOf(now));
  });
});
