import { expect, test } from '@playwright/test';
import { nextSlots, startOpensAt } from './support/slots';

const iso = (d: Date) => d.toISOString().replace('.000', '');

test.describe('slot grid', () => {
  test('the first slots are two hours and a margin away, on the 15-minute grid', () => {
    const slots = nextSlots(new Date('2026-11-14T08:53:40Z'), 3);
    expect(slots.map(iso)).toEqual(['2026-11-14T11:15:00Z', '2026-11-14T11:30:00Z', '2026-11-14T11:45:00Z']);
  });

  test('a lead that lands before 08:00 Prague moves to the opening (winter, UTC+1)', () => {
    expect(iso(nextSlots(new Date('2026-11-14T03:00:00Z'), 1)[0])).toBe('2026-11-14T07:00:00Z');
  });

  test('a lead that lands before 08:00 Prague moves to the opening (summer, UTC+2)', () => {
    expect(iso(nextSlots(new Date('2026-07-01T03:00:00Z'), 1)[0])).toBe('2026-07-01T06:00:00Z');
  });

  test('a lead past 19:45 Prague moves to the next morning', () => {
    expect(iso(nextSlots(new Date('2026-11-14T17:00:00Z'), 1)[0])).toBe('2026-11-15T07:00:00Z');
  });

  test('slots that run past the last start continue the next morning', () => {
    const slots = nextSlots(new Date('2026-11-14T16:20:00Z'), 2);
    expect(slots.map(iso)).toEqual(['2026-11-14T18:45:00Z', '2026-11-15T07:00:00Z']);
  });

  test('a time already on the grid is kept', () => {
    expect(iso(nextSlots(new Date('2026-11-14T08:55:00Z'), 1)[0])).toBe('2026-11-14T11:15:00Z');
  });

  test('a job can be started from 60 minutes before its slot', () => {
    expect(iso(startOpensAt(new Date('2026-11-14T11:15:00Z')))).toBe('2026-11-14T10:15:00Z');
  });
});
