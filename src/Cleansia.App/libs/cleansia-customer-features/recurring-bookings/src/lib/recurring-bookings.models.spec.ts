import { DirtinessLevel } from '@cleansia/customer-services';
import {
  missingFields,
  nextOccurrenceUtc,
  RECURRING_WIZARD_INITIAL_DATA,
  RecurrenceFrequency,
  RecurringWizardFormData,
} from './recurring-bookings.models';

/**
 * The web's copy of `ComputeOccurrences`. A schedule's "Wednesday 10:00" is
 * 10:00 where the home is, so the next run is 08:00Z in a Prague summer and
 * 09:00Z in its winter — the same instants the backend's tests pin.
 */
describe('nextOccurrenceUtc', () => {
  const prague = 'Europe/Prague';

  const weekly = (dayOfWeek: number, timeOfDay: string, lastMaterializedFor?: string) => ({
    frequency: RecurrenceFrequency.Weekly,
    dayOfWeek,
    timeOfDay,
    startsOn: '2026-01-01T00:00:00Z',
    lastMaterializedFor,
  });

  const wednesday = 3;
  const thursday = 4;
  const tuesday = 2;
  const sunday = 0;

  it('is ten o’clock Prague in summer', () => {
    const next = nextOccurrenceUtc(weekly(wednesday, '10:00'), new Date('2026-07-06T00:00:00Z'), prague);

    expect(next?.toISOString()).toBe('2026-07-08T08:00:00.000Z');
  });

  it('is ten o’clock Prague in winter', () => {
    const next = nextOccurrenceUtc(weekly(wednesday, '10:00'), new Date('2026-01-05T00:00:00Z'), prague);

    expect(next?.toISOString()).toBe('2026-01-07T09:00:00.000Z');
  });

  it('keeps the hour when resuming across the spring change', () => {
    const next = nextOccurrenceUtc(
      weekly(wednesday, '10:00', '2026-03-25T09:00:00Z'),
      new Date('2026-03-23T00:00:00Z'),
      prague,
    );

    expect(next?.toISOString()).toBe('2026-04-01T08:00:00.000Z');
  });

  it('keeps the hour on the Sunday the clocks go back', () => {
    const next = nextOccurrenceUtc(weekly(sunday, '10:00'), new Date('2026-10-24T00:00:00Z'), prague);

    expect(next?.toISOString()).toBe('2026-10-25T09:00:00.000Z');
  });

  it('moves a time the spring change skips forward by the gap, as the backend does', () => {
    const next = nextOccurrenceUtc(weekly(sunday, '02:30'), new Date('2026-03-27T00:00:00Z'), prague);

    expect(next?.toISOString()).toBe('2026-03-29T01:30:00.000Z');
  });

  it('reads a time the autumn change repeats as the later, standard-time one', () => {
    const next = nextOccurrenceUtc(weekly(sunday, '02:30'), new Date('2026-10-23T00:00:00Z'), prague);

    expect(next?.toISOString()).toBe('2026-10-25T01:30:00.000Z');
  });

  it('reads the weekday in the market’s calendar', () => {
    // 22:30Z on Tuesday is already Wednesday in Prague, so Tuesday's 23:00 is a week away.
    const next = nextOccurrenceUtc(weekly(tuesday, '23:00'), new Date('2026-07-07T22:30:00Z'), prague);

    expect(next?.toISOString()).toBe('2026-07-14T21:00:00.000Z');
  });

  it('walks UTC when the zone is UTC', () => {
    const next = nextOccurrenceUtc(weekly(wednesday, '10:00'), new Date('2026-07-06T00:00:00Z'), 'UTC');

    expect(next?.toISOString()).toBe('2026-07-08T10:00:00.000Z');
  });

  it('has no next run once the schedule has ended', () => {
    const next = nextOccurrenceUtc(
      { ...weekly(wednesday, '10:00'), endsOn: '2026-07-07T00:00:00Z' },
      new Date('2026-07-06T00:00:00Z'),
      prague,
    );

    expect(next).toBeNull();
  });

  it('skips a visit less than two hours away', () => {
    const next = nextOccurrenceUtc(weekly(wednesday, '10:00'), new Date('2026-07-08T07:00:00Z'), prague);

    expect(next?.toISOString()).toBe('2026-07-15T08:00:00.000Z');
  });

  it('reads the schedule in the zone the template names', () => {
    const next = nextOccurrenceUtc(
      { ...weekly(wednesday, '10:00'), timeZoneId: 'America/New_York' },
      new Date('2026-07-06T00:00:00Z'),
    );

    expect(next?.toISOString()).toBe('2026-07-08T14:00:00.000Z');
  });

  it('keeps a fortnightly schedule on its own weeks after an edit', () => {
    const next = nextOccurrenceUtc(
      {
        frequency: RecurrenceFrequency.Biweekly,
        dayOfWeek: thursday,
        timeOfDay: '10:00',
        startsOn: '2026-01-01T00:00:00Z',
      },
      new Date('2026-07-06T00:00:00Z'),
      prague,
    );

    expect(next?.toISOString()).toBe('2026-07-16T08:00:00.000Z');
  });

  describe('monthly', () => {
    const monthly = (startsOn: string, lastMaterializedFor?: string) => ({
      frequency: RecurrenceFrequency.Monthly,
      dayOfWeek: thursday,
      timeOfDay: '10:00',
      startsOn,
      lastMaterializedFor,
    });

    it('keeps the second Thursday of the month', () => {
      const next = nextOccurrenceUtc(
        monthly('2026-10-08T00:00:00Z', '2026-11-12T09:00:00Z'),
        new Date('2026-11-13T00:00:00Z'),
        prague,
      );

      expect(next?.toISOString()).toBe('2026-12-10T09:00:00.000Z');
    });

    it('takes the fifth Thursday in a month that has five', () => {
      const next = nextOccurrenceUtc(
        monthly('2026-10-29T00:00:00Z', '2026-11-26T09:00:00Z'),
        new Date('2026-11-27T00:00:00Z'),
        prague,
      );

      expect(next?.toISOString()).toBe('2026-12-31T09:00:00.000Z');
    });

    it('takes the last Thursday in a month that has only four', () => {
      const next = nextOccurrenceUtc(
        monthly('2026-10-29T00:00:00Z', '2026-10-29T09:00:00Z'),
        new Date('2026-10-30T00:00:00Z'),
        prague,
      );

      expect(next?.toISOString()).toBe('2026-11-26T09:00:00.000Z');
    });

    it('keeps the cadence after an edit clears the last visit', () => {
      const next = nextOccurrenceUtc(monthly('2026-10-08T00:00:00Z'), new Date('2026-11-16T00:00:00Z'), prague);

      expect(next?.toISOString()).toBe('2026-12-10T09:00:00.000Z');
    });
  });
});

describe('missingFields', () => {
  const complete: RecurringWizardFormData = {
    ...RECURRING_WIZARD_INITIAL_DATA,
    selectedServiceIds: ['s1'],
    dirtinessLevel: DirtinessLevel.Normal,
    savedAddressId: 'addr-1',
    startsOn: new Date('2026-10-01T00:00:00Z'),
  };

  it('asks a new schedule for the request to start within the withdrawal period until it is made', () => {
    expect(RECURRING_WIZARD_INITIAL_DATA.earlyPerformanceRequested).toBe(false);
    expect(missingFields(complete, true)).toEqual(['earlyPerformance']);
    expect(missingFields({ ...complete, earlyPerformanceRequested: true }, true)).toEqual([]);
  });

  it('does not ask an edit, whose schedule recorded the request when it was set up', () => {
    expect(missingFields(complete, false)).toEqual([]);
  });

  it('holds a schedule whose terms tick is asked until it is ticked, in the order the form asks', () => {
    expect(RECURRING_WIZARD_INITIAL_DATA.termsAccepted).toBe(false);
    expect(missingFields(complete, true, true)).toEqual(['terms', 'earlyPerformance']);
    expect(
      missingFields({ ...complete, termsAccepted: true, earlyPerformanceRequested: true }, true, true),
    ).toEqual([]);
  });

  it('does not hold on a tick it does not ask for, however it was left', () => {
    expect(missingFields({ ...complete, earlyPerformanceRequested: true }, true, false)).toEqual([]);
  });
});
