import { nextSlots, pragueSlot } from './slots';

// The phase-2 order plan (T-0934, BRIEF §2.4) as data. The brief's table is in UTC; the slots here are the same
// instants as Prague wall-clock times (UTC+1 in November), because that is how the booking window reads them.
// Who is who: cleaner = A, cleaner2 = B, cleaner3 = E. orders/10 books every order on Day 1 from 07:30 UTC.

export type PlanDay = 'D1' | 'D1+1' | 'D2';

/** Owner ruling 2026-11-15: Day 1 is Wed 18 Nov, Day 2 Thu 19 Nov, so Day 1 + 1 is Day 2's date. */
export const RULED_DAYS: Record<PlanDay, string> = { D1: '2026-11-18', 'D1+1': '2026-11-19', D2: '2026-11-19' };

/** The throwaway two-cleaner service the Manager creates in people/50 (run.serviceId): 119 min, 1 per room. */
export const THROWAWAY = 'throwaway';

export type PlanCleaner = 'cleaner' | 'cleaner2' | 'cleaner3';

export interface PlannedOrder {
  key: string;
  /** The account that books it; with `guest` the booking is a guest's that uses this account's e-mail. */
  customer: 'customer' | 'nocard' | 'first' | 'debtor' | 'cashcap';
  guest?: true;
  payment: 'card' | 'cash';
  /** A card order the plan pays in Stripe test Checkout; false leaves it unpaid (refused probes, stale sweeps). */
  pay: boolean;
  /** Catalogue service names, or THROWAWAY. */
  services: string[];
  extras?: string[];
  rooms: number;
  bathrooms: number;
  /** DirtinessLevel: 0 Normal, 1 Increased, 2 Heavy. */
  level: 0 | 1 | 2;
  /** A Prague time on a plan day, or 'express': the first grid slot 3 hours or more after the booking. */
  slot: { day: PlanDay; prague: string } | 'express';
  /** The cleaners who end up taking it (a race lists both racers). */
  crew: PlanCleaner[];
  race?: true;
  accessInstructions?: string;
  saveCard?: boolean;
  /** A tier order: the fee rate its cancel at 07:58-08:15 on Day 1 is priced at. */
  cancelFee?: 0 | 0.25 | 0.5;
}

const BATHROOM = 'Bathroom Cleaning';
const base = { pay: true, services: [BATHROOM], rooms: 1, bathrooms: 1, level: 0, crew: [] } as const;
const order = (o: Partial<PlannedOrder> & Pick<PlannedOrder, 'key' | 'customer' | 'payment' | 'slot'>): PlannedOrder =>
  ({ ...base, ...o, services: [...(o.services ?? base.services)], crew: [...(o.crew ?? [])] }) as PlannedOrder;
const d1 = (prague: string) => ({ day: 'D1', prague }) as const;
const d1plus1 = (prague: string) => ({ day: 'D1+1', prague }) as const;

export const ORDER_PLAN: readonly PlannedOrder[] = [
  order({ key: 'tierA', customer: 'customer', payment: 'card', slot: d1plus1('11:00'), crew: ['cleaner'], cancelFee: 0 }),
  order({ key: 'tierB', customer: 'customer', payment: 'card', services: ['General Cleaning'], rooms: 2, bathrooms: 1, level: 1, slot: d1('19:45'), crew: ['cleaner'], cancelFee: 0.25 }),
  order({ key: 'tierC', customer: 'customer', payment: 'card', slot: d1('12:15'), crew: ['cleaner'], cancelFee: 0.5 }),
  order({ key: 'X', customer: 'customer', payment: 'card', services: [BATHROOM, 'Kitchen Deep Clean', 'Window Cleaning'], extras: ['inside-fridge'], slot: d1('13:00'), crew: ['cleaner'] }),
  order({ key: 'Y', customer: 'customer', payment: 'card', slot: d1('13:00') }),
  order({ key: 'Z', customer: 'customer', payment: 'card', slot: d1('13:15'), crew: ['cleaner'] }),
  order({ key: 'W', customer: 'customer', payment: 'card', services: [THROWAWAY], slot: d1('15:30'), crew: ['cleaner', 'cleaner2'], accessInstructions: 'key under the mat' }),
  order({ key: 'P', customer: 'customer', payment: 'card', slot: d1('17:45'), crew: ['cleaner'] }),
  order({ key: 'P2', customer: 'customer', payment: 'card', slot: d1('18:15'), crew: ['cleaner'] }),
  order({ key: 'R1', customer: 'nocard', payment: 'card', saveCard: false, slot: d1('18:30'), crew: ['cleaner2', 'cleaner3'], race: true }),
  order({ key: 'R2', customer: 'customer', payment: 'card', slot: d1('18:45'), crew: ['cleaner2', 'cleaner3'], race: true }),
  order({ key: 'R3', customer: 'customer', payment: 'card', slot: d1('19:00'), crew: ['cleaner2', 'cleaner3'], race: true }),
  order({ key: 'F1', customer: 'first', payment: 'card', saveCard: true, slot: 'express', crew: ['cleaner3'] }),
  order({ key: 'F2', customer: 'first', payment: 'cash', slot: d1plus1('11:00') }),
  order({ key: 'D1', customer: 'debtor', payment: 'cash', slot: d1('12:45'), crew: ['cleaner'] }),
  order({ key: 'D2', customer: 'debtor', payment: 'cash', slot: d1('15:00'), crew: ['cleaner'] }),
  order({ key: 'D3', customer: 'debtor', payment: 'cash', slot: d1plus1('11:15') }),
  order({ key: 'D4', customer: 'debtor', payment: 'card', pay: false, slot: d1plus1('11:30') }),
  order({ key: 'G1', customer: 'debtor', guest: true, payment: 'card', pay: false, slot: d1plus1('11:45') }),
  order({ key: 'K1', customer: 'cashcap', payment: 'cash', services: [THROWAWAY], bathrooms: 0, slot: d1plus1('11:00') }),
  order({ key: 'K2', customer: 'cashcap', payment: 'cash', slot: d1plus1('11:15') }),
  order({ key: 'K3', customer: 'cashcap', payment: 'card', pay: false, slot: d1plus1('11:30') }),
  order({ key: 'C3', customer: 'cashcap', payment: 'cash', slot: d1('13:30'), crew: ['cleaner'] }),
  order({ key: 'B18', customer: 'customer', payment: 'card', pay: false, slot: d1plus1('10:15') }),
  order({ key: 'J1', customer: 'customer', payment: 'card', slot: { day: 'D2', prague: '09:00' }, crew: ['cleaner3'] }),
  order({ key: 'J2', customer: 'customer', payment: 'card', slot: { day: 'D2', prague: '09:15' }, crew: ['cleaner3'] }),
];

export const planned = (key: string): PlannedOrder => {
  const o = ORDER_PLAN.find((x) => x.key === key);
  if (!o) throw new Error(`no planned order ${key}`);
  return o;
};

/** The order's start: its plan day and Prague time, or for 'express' the first grid slot 3 h or more after `bookedAt`. */
export function slotOf(o: PlannedOrder, bookedAt: Date, days: Record<PlanDay, string> = RULED_DAYS): Date {
  return o.slot === 'express' ? nextSlots(bookedAt, 1, 180)[0] : pragueSlot(days[o.slot.day], o.slot.prague);
}

/** Booked minutes on DEV: every seeded service is 2 minutes (F-031); the throwaway is 119 plus 1 per room and bathroom. */
export const plannedMinutes = (o: PlannedOrder) =>
  o.services.reduce((sum, s) => sum + (s === THROWAWAY ? 119 + o.rooms + o.bathrooms : 2), 0);
