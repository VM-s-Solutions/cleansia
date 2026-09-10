import { generateTimeOptions } from './booking-window.models';

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
