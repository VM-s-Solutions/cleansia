import { formatHours, HOUR_TICK_STEP_MINUTES } from './time-analytics.helpers';

describe('formatHours', () => {
  it('prints minutes as hours with the unit of the session language', () => {
    expect(formatHours(120, 'en')).toBe('2 hr');
    expect(formatHours(120, 'cs')).toBe('2 h');
    expect(formatHours(120, 'uk')).toBe('2 год');
    expect(formatHours(120, 'ru')).toBe('2 ч');
  });

  it('keeps one decimal for a summary and none for an axis tick', () => {
    expect(formatHours(90, 'en')).toBe('1.5 hr');
    expect(formatHours(90, 'cs')).toBe('1,5 h');
    expect(formatHours(90, 'en', 0)).toBe('2 hr');
    expect(formatHours(45, 'en', 0)).toBe('1 hr');
  });

  it('reads nothing as zero', () => {
    expect(formatHours(undefined, 'en')).toBe('0 hr');
    expect(formatHours(Number.NaN, 'cs')).toBe('0 h');
  });

  // The axis is in minutes; a tick every 60 lands each label on a whole hour, so two ticks can
  // never round to the same "1 hr".
  it('spaces the axis one hour apart', () => {
    expect(HOUR_TICK_STEP_MINUTES).toBe(60);
  });
});
