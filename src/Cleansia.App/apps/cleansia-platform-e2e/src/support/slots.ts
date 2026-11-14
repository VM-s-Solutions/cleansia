// The DEV booking window: a start sits on the 15-minute grid, from 08:00 to 19:45 in Prague, at least 2 hours
// away (/product/business-rules); a job can be started from 60 minutes before its slot.
const MINUTE = 60_000;
const GRID = 15 * MINUTE;
const OPENS = 8 * 60;
const LAST_START = 19 * 60 + 45;
const pragueClock = new Intl.DateTimeFormat('en-GB', { timeZone: 'Europe/Prague', hour: '2-digit', minute: '2-digit', hourCycle: 'h23' });

function pragueMinutes(at: Date): number {
  const [h, m] = pragueClock.format(at).split(':').map(Number);
  return h * 60 + m;
}

/** The next `count` bookable slots, 15 minutes apart, the first at least `leadMinutes` from `now`. */
export function nextSlots(now: Date, count: number, leadMinutes = 140): Date[] {
  let t = Math.ceil((now.getTime() + leadMinutes * MINUTE) / GRID) * GRID;
  const slots: Date[] = [];
  while (slots.length < count) {
    const local = pragueMinutes(new Date(t));
    if (local < OPENS) t += (OPENS - local) * MINUTE;
    else if (local > LAST_START) t += (24 * 60 - local + OPENS) * MINUTE;
    else {
      slots.push(new Date(t));
      t += GRID;
    }
  }
  return slots;
}

export const startOpensAt = (slot: Date) => new Date(slot.getTime() - 60 * MINUTE);
