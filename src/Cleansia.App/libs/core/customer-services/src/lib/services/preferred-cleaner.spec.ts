import { GetMyServingCleanersResponse } from '../client/customer-client';
import {
  survivingPreferredSelection,
  toPreferredCleanerOptions,
} from './preferred-cleaner';

const UNAVAILABLE = 'Not available for this date and time';

function cleaner(fields: {
  employeeId?: string;
  fullName?: string;
  isAvailableForRequestedSlot?: boolean;
}): GetMyServingCleanersResponse {
  const row = new GetMyServingCleanersResponse();
  row.employeeId = 'employeeId' in fields ? fields.employeeId : 'emp-1';
  row.fullName = 'fullName' in fields ? fields.fullName : 'Anna Nováková';
  row.isAvailableForRequestedSlot = fields.isAvailableForRequestedSlot;
  return row;
}

describe('toPreferredCleanerOptions', () => {
  it('offers an unevaluated row as an ordinary choice', () => {
    const options = toPreferredCleanerOptions(
      [cleaner({ isAvailableForRequestedSlot: undefined })],
      UNAVAILABLE
    );

    expect(options).toEqual([
      { label: 'Anna Nováková', value: 'emp-1', disabled: false },
    ]);
  });

  it('offers an available row as an ordinary choice', () => {
    const options = toPreferredCleanerOptions(
      [cleaner({ isAvailableForRequestedSlot: true })],
      UNAVAILABLE
    );

    expect(options[0].disabled).toBe(false);
    expect(options[0].label).toBe('Anna Nováková');
  });

  it('keeps an unavailable row visible, unselectable and labelled with the neutral line', () => {
    const options = toPreferredCleanerOptions(
      [cleaner({ isAvailableForRequestedSlot: false })],
      UNAVAILABLE
    );

    expect(options).toEqual([
      {
        label: `Anna Nováková · ${UNAVAILABLE}`,
        value: 'emp-1',
        disabled: true,
      },
    ]);
  });

  it('drops a row that carries no id or no name — neither can be chosen or read', () => {
    const options = toPreferredCleanerOptions(
      [
        cleaner({ employeeId: undefined }),
        cleaner({ employeeId: 'emp-2', fullName: '   ' }),
        cleaner({ employeeId: 'emp-3', fullName: 'Petr Svoboda' }),
      ],
      UNAVAILABLE
    );

    expect(options).toEqual([
      { label: 'Petr Svoboda', value: 'emp-3', disabled: false },
    ]);
  });
});

describe('survivingPreferredSelection', () => {
  const roster = [
    cleaner({ employeeId: 'emp-1', isAvailableForRequestedSlot: true }),
    cleaner({ employeeId: 'emp-2', isAvailableForRequestedSlot: false }),
    cleaner({ employeeId: 'emp-3', isAvailableForRequestedSlot: undefined }),
  ];

  it('keeps a selection the slot still admits', () => {
    expect(survivingPreferredSelection(roster, 'emp-1')).toBe('emp-1');
  });

  it('keeps a selection the slot was never asked about', () => {
    expect(survivingPreferredSelection(roster, 'emp-3')).toBe('emp-3');
  });

  it('clears a selection the slot no longer admits', () => {
    expect(survivingPreferredSelection(roster, 'emp-2')).toBeNull();
  });

  it('clears a selection that has left the roster', () => {
    expect(survivingPreferredSelection(roster, 'emp-9')).toBeNull();
  });

  it('leaves an empty selection empty', () => {
    expect(survivingPreferredSelection(roster, null)).toBeNull();
  });
});
