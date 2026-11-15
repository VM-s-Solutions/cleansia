import { expect, test } from '@playwright/test';
import { ORDER_PLAN, planned, plannedMinutes, RULED_DAYS, slotOf } from './support/plan';
import { nextSlots, noticeHours, pragueSlot } from './support/slots';

// The order plan of T-0934 (BRIEF §2.4) on the ruled days: Day 1 Wed 18 Nov, Day 2 Thu 19 Nov (owner ruling 2026-11-15).
// orders/10 books every order between 07:30 and 08:00 UTC on Day 1; orders/30 cancels the tier orders 07:58-08:15.
const MINUTE = 60_000;
const at = (hhmm: string) => new Date(`${RULED_DAYS.D1}T${hhmm}:00Z`);
const BOOKING_FROM = at('07:30');
const BOOKING_TO = at('08:00');
const everyMinute = (from: Date, to: Date) =>
  Array.from({ length: (to.getTime() - from.getTime()) / MINUTE + 1 }, (_, i) => new Date(from.getTime() + i * MINUTE));
const pragueClock = new Intl.DateTimeFormat('en-GB', { timeZone: 'Europe/Prague', hour: '2-digit', minute: '2-digit', hourCycle: 'h23' });
const pragueMinutes = (d: Date) => pragueClock.format(d).split(':').map(Number).reduce((h, m) => h * 60 + m);
// The notice tiers frozen on every order (/product/business-rules#cancellation): 24 h and more free, 4-24 h 25 %, under 4 h 50 %.
const tierRate = (notice: number) => (notice >= 24 ? 0 : notice >= 4 ? 0.25 : 0.5);
const fixed = ORDER_PLAN.filter((o) => o.slot !== 'express');

test.describe('order plan', () => {
  test('the plan holds every order of the brief once', () => {
    const keys = ORDER_PLAN.map((o) => o.key);
    expect(new Set(keys).size).toBe(keys.length);
    expect([...keys].sort()).toEqual(
      ['B18', 'C3', 'D1', 'D2', 'D3', 'D4', 'F1', 'F2', 'G1', 'J1', 'J2', 'K1', 'K2', 'K3', 'P', 'P2', 'R1', 'R2', 'R3', 'W', 'X', 'Y', 'Z', 'tierA', 'tierB', 'tierC'].sort(),
    );
  });

  test('the ruled days: Day 1 is Wed 18 Nov, Day 1 + 1 and Day 2 are Thu 19 Nov', () => {
    expect(RULED_DAYS).toEqual({ D1: '2026-11-18', 'D1+1': '2026-11-19', D2: '2026-11-19' });
  });

  test('every planned slot is on the 15-minute grid, from 08:00 to 19:45 Prague, inside 60 days and at least 2 h after the bookings', () => {
    for (const o of fixed) {
      const slot = slotOf(o, BOOKING_TO);
      expect(slot.getTime() % (15 * MINUTE), o.key).toBe(0);
      expect(pragueMinutes(slot), o.key).toBeGreaterThanOrEqual(8 * 60);
      expect(pragueMinutes(slot), o.key).toBeLessThanOrEqual(19 * 60 + 45);
      expect(slot.getTime() - BOOKING_FROM.getTime(), o.key).toBeLessThanOrEqual(60 * 24 * 60 * MINUTE);
      expect(noticeHours(slot, BOOKING_TO), o.key).toBeGreaterThanOrEqual(2);
    }
  });

  test('the slots are the brief\'s UTC times, read as Prague time on the ruled days', () => {
    const utc = (key: string) => slotOf(planned(key), BOOKING_TO).toISOString().slice(0, 16);
    expect([utc('tierA'), utc('tierB'), utc('tierC'), utc('W'), utc('B18'), utc('J1'), utc('J2')]).toEqual([
      '2026-11-19T10:00', '2026-11-18T18:45', '2026-11-18T11:15', '2026-11-18T14:30', '2026-11-19T09:15', '2026-11-19T08:00', '2026-11-19T08:15',
    ]);
  });

  test('no two jobs of the same cleaner overlap, the phase-1 jobs of A and B included (2 min per service, W 121)', () => {
    expect(plannedMinutes(planned('W'))).toBe(121);
    expect(plannedMinutes(planned('K1'))).toBe(120);
    // The phase-1 book stage of the suite run starts at 07:00 Day 1: A takes its first two slots, B the web half's first two.
    const phase1 = nextSlots(at('07:00'), 2);
    const jobs = [
      ...phase1.map((start) => ({ key: 'phase-1 API', cleaner: 'cleaner', start, minutes: 2 })),
      ...phase1.map((start) => ({ key: 'phase-1 web', cleaner: 'cleaner2', start, minutes: 2 })),
      ...ORDER_PLAN.flatMap((o) => o.crew.map((cleaner) => ({ key: o.key, cleaner, start: slotOf(o, BOOKING_FROM), minutes: plannedMinutes(o) }))),
    ];
    for (const a of jobs) {
      for (const b of jobs) {
        if (a === b || a.cleaner !== b.cleaner) continue;
        const overlap = a.start.getTime() < b.start.getTime() + b.minutes * MINUTE && b.start.getTime() < a.start.getTime() + a.minutes * MINUTE;
        expect(overlap, `${a.cleaner}: ${a.key} at ${a.start.toISOString()} and ${b.key} at ${b.start.toISOString()}`).toBe(false);
      }
    }
  });

  test('the tier cancels made 07:58-08:15 land free, 25 % and 50 % for bookings made 07:30-08:00', () => {
    const tiers = ORDER_PLAN.filter((o) => o.cancelFee !== undefined);
    expect(tiers.map((o) => [o.key, o.cancelFee])).toEqual([['tierA', 0], ['tierB', 0.25], ['tierC', 0.5]]);
    for (const o of tiers) {
      for (const bookedAt of everyMinute(BOOKING_FROM, BOOKING_TO)) {
        const slot = slotOf(o, bookedAt);
        expect(noticeHours(slot, bookedAt), `${o.key} booked ${bookedAt.toISOString()}`).toBeGreaterThanOrEqual(2);
        for (const cancelAt of everyMinute(at('07:58'), at('08:15'))) {
          expect(tierRate(noticeHours(slot, cancelAt)), `${o.key} cancelled ${cancelAt.toISOString()}`).toBe(o.cancelFee);
        }
      }
    }
  });

  test('F1 is express: booked any minute 07:30-08:00, its slot is on the grid 2 to 4 hours away', () => {
    const f1 = planned('F1');
    expect(f1.slot).toBe('express');
    for (const bookedAt of everyMinute(BOOKING_FROM, BOOKING_TO)) {
      const slot = slotOf(f1, bookedAt);
      expect(slot.getTime() % (15 * MINUTE)).toBe(0);
      expect(noticeHours(slot, bookedAt)).toBeGreaterThanOrEqual(2);
      expect(noticeHours(slot, bookedAt)).toBeLessThan(4);
    }
  });

  test('a day passed in replaces the ruled one, so D2 can follow the period DEV closed', () => {
    const j1 = planned('J1');
    expect(slotOf(j1, BOOKING_TO, { ...RULED_DAYS, D2: '2026-11-20' })).toEqual(pragueSlot('2026-11-20', '09:00'));
  });
});
