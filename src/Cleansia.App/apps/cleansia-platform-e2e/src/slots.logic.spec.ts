import { expect, test } from '@playwright/test';
import { crc32 } from 'node:zlib';
import { png, retryAfterMs } from './support/dev';
import { dayAfter, nextSlots, noticeHours, pragueSlot, startOpensAt, wizardLabels } from './support/slots';

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

test.describe('named slots', () => {
  test('a Prague time on a winter day is an hour ahead of UTC', () => {
    expect(iso(pragueSlot('2026-11-18', '19:45'))).toBe('2026-11-18T18:45:00Z');
    expect(iso(pragueSlot('2026-11-19', '08:00'))).toBe('2026-11-19T07:00:00Z');
  });

  test('a Prague time on a summer day is two hours ahead of UTC', () => {
    expect(iso(pragueSlot('2026-07-01', '08:00'))).toBe('2026-07-01T06:00:00Z');
  });

  test('the day after rolls over the month and the year', () => {
    expect([dayAfter('2026-11-18'), dayAfter('2026-11-30'), dayAfter('2026-12-31')]).toEqual(['2026-11-19', '2026-12-01', '2027-01-01']);
  });

  test('the notice is the hours from an instant to the slot, fractional', () => {
    const slot = pragueSlot('2026-11-18', '19:45');
    expect(noticeHours(slot, new Date('2026-11-17T18:45:00Z'))).toBe(24);
    expect(noticeHours(slot, new Date('2026-11-18T08:15:00Z'))).toBe(10.5);
  });
});

test.describe('booking wizard labels', () => {
  test('a winter evening slot reads in Prague time', () => {
    expect(wizardLabels(new Date('2026-11-14T17:45:00Z'))).toEqual({ day: 'Saturday, November 14, 2026', part: 'Evening', time: '18:45' });
  });

  test('the day follows Prague, not UTC, around midnight', () => {
    expect(wizardLabels(new Date('2026-11-14T23:30:00Z')).day).toBe('Sunday, November 15, 2026');
  });

  test('the parts of the day split at 12:00 and 16:00', () => {
    const part = (utc: string) => wizardLabels(new Date(`2026-11-14T${utc}Z`)).part;
    expect([part('07:00:00'), part('10:45:00'), part('11:00:00'), part('14:45:00'), part('15:00:00')]).toEqual(['Morning', 'Morning', 'Afternoon', 'Afternoon', 'Evening']);
  });
});

test.describe('rate-limit backoff', () => {
  const now = Date.parse('2026-11-18T07:00:00Z');

  test('Retry-After in seconds is waited in full', () => {
    expect(retryAfterMs('12', now)).toBe(12_000);
  });

  test('Retry-After as an HTTP date is waited until that instant', () => {
    expect(retryAfterMs('Wed, 18 Nov 2026 07:00:20 GMT', now)).toBe(20_000);
  });

  test('no header, a past date or garbage waits 30 s, and nothing waits under 1 s', () => {
    expect([retryAfterMs(undefined, now), retryAfterMs('soon', now)]).toEqual([30_000, 30_000]);
    expect(retryAfterMs('Wed, 18 Nov 2026 06:59:00 GMT', now)).toBe(1_000);
    expect(retryAfterMs('0', now)).toBe(1_000);
  });
});

test.describe('upload fixtures', () => {
  test('png() is a 16x16 PNG whose chunks carry valid CRCs, and two calls differ', () => {
    const bytes = Buffer.from(png(), 'base64');
    expect([...bytes.subarray(0, 8)]).toEqual([137, 80, 78, 71, 13, 10, 26, 10]);
    expect(bytes.toString('ascii', 12, 16)).toBe('IHDR');
    expect([bytes.readUInt32BE(16), bytes.readUInt32BE(20)]).toEqual([16, 16]);
    let at = 8;
    while (at < bytes.length) {
      const len = bytes.readUInt32BE(at);
      expect(bytes.readUInt32BE(at + 8 + len)).toBe(crc32(bytes.subarray(at + 4, at + 8 + len)));
      at += len + 12;
    }
    expect(at).toBe(bytes.length);
    expect(png()).not.toBe(png());
  });
});
