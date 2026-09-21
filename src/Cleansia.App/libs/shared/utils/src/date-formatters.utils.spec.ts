import { formatDate } from './date-formatters.utils';

const noon = new Date(2026, 8, 21, 10, 30);

describe('formatDate', () => {
  it('prints the day in the language of the session, not in en-GB', () => {
    expect(formatDate(noon, 'cs')).toBe('21. 9. 2026');
    expect(formatDate(noon, 'sk')).toBe('21. 9. 2026');
    expect(formatDate(noon, 'en')).toBe('Sep 21, 2026');
    expect(formatDate(noon, 'uk')).toBe('21 вер. 2026 р.');
  });

  it('adds the time to the minute, never the seconds, when asked for a stamp', () => {
    expect(formatDate(noon, 'cs', 'dateTime')).toBe('21. 9. 2026 10:30');
    expect(formatDate(noon, 'en', 'dateTime')).toBe('Sep 21, 2026, 10:30 AM');
    expect(formatDate(noon, 'uk', 'dateTime')).toBe('21 вер. 2026 р., 10:30');
  });

  it('reads a calendar day carried as midnight UTC on that day, whatever the local zone', () => {
    expect(formatDate(new Date('2026-09-21T00:00:00Z'), 'cs', 'utcDate')).toBe('21. 9. 2026');
    expect(formatDate(new Date('2026-09-21T00:00:00Z'), 'en', 'utcDate')).toBe('Sep 21, 2026');
  });

  it('parses the ISO string the wire carries when the client did not', () => {
    expect(formatDate('2026-09-21T10:30:00', 'cs', 'dateTime')).toBe('21. 9. 2026 10:30');
  });

  it('prints nothing for no date and for a date that does not parse', () => {
    expect(formatDate(null, 'cs')).toBe('');
    expect(formatDate(undefined, 'cs')).toBe('');
    expect(formatDate('not a date', 'cs')).toBe('');
  });

  it('falls back to the default locale for an unknown language', () => {
    expect(formatDate(noon, undefined)).toBe('Sep 21, 2026');
  });
});
