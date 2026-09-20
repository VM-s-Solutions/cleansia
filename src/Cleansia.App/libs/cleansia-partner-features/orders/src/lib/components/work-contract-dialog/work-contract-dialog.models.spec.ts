import {
  AcceptWorkContractCommand,
  TakeOrderCommand,
  WorkContractFacts,
} from '@cleansia/partner-services';
import {
  buildAcceptWorkContractCommand,
  buildTakeOrderCommand,
  buildWorkContractFactRows,
  formatCleaningWindow,
  languageDisplayName,
} from './work-contract-dialog.models';

const ORDER_ID = 'ord-1';
const TEXT_ID = 'text-1';

function facts(overrides: Partial<Record<string, unknown>> = {}): WorkContractFacts {
  return WorkContractFacts.fromJS({
    orderNumber: 'CLS-42',
    cleaningDateTimeUtc: '2026-10-03T08:30:00Z',
    estimatedMinutes: 150,
    totalPrice: 1250,
    currencyCode: 'CZK',
    locationApproximate: 'Praha 6, 160 00',
    countryId: 'CZ',
    rooms: 3,
    bathrooms: 1,
    services: [{ id: 's1', name: 'Window cleaning' }],
    packages: [{ id: 'p1', name: 'Move-out bundle' }],
    extraSlugs: ['fridge', 'oven'],
    ...overrides,
  });
}

describe('formatCleaningWindow', () => {
  it('renders the start and the end the estimate implies, on one line', () => {
    const start = new Date(2026, 9, 3, 10, 30);

    expect(formatCleaningWindow(start, 150)).toBe('03.10.2026 10:30 – 13:00');
  });

  it('renders only the start when the estimate is missing', () => {
    const start = new Date(2026, 9, 3, 10, 30);

    expect(formatCleaningWindow(start, 0)).toBe('03.10.2026 10:30');
  });

  it('renders nothing without a start', () => {
    expect(formatCleaningWindow(undefined, 90)).toBe('');
  });
});

describe('buildWorkContractFactRows', () => {
  it('lists the frozen job facts in the order the contract names them', () => {
    const rows = buildWorkContractFactRows(facts(), 'en');

    expect(rows.map((row) => row.labelKey)).toEqual([
      'pages.orders.work_contract.facts.order_number',
      'pages.orders.work_contract.facts.window',
      'pages.orders.work_contract.facts.price',
      'pages.orders.work_contract.facts.location',
      'pages.orders.work_contract.facts.rooms_bathrooms',
      'pages.orders.work_contract.facts.services',
      'pages.orders.work_contract.facts.packages',
      'pages.orders.work_contract.facts.extras',
    ]);
    expect(rows[0].value).toBe('CLS-42');
    expect(rows[2].value).toBe('CZK 1,250');
    expect(rows[3].value).toBe('Praha 6, 160 00');
    expect(rows[4].value).toBe('3 / 1');
    expect(rows[5].value).toBe('Window cleaning');
    expect(rows[6].value).toBe('Move-out bundle');
    expect(rows[7].value).toBe('fridge, oven');
  });

  it('formats the price in the UI language', () => {
    const rows = buildWorkContractFactRows(facts(), 'cs');

    expect(rows[2].value).toBe('1 250 Kč');
  });

  it('drops the scope rows the job does not have', () => {
    const rows = buildWorkContractFactRows(
      facts({ services: [], packages: [], extraSlugs: [] }),
      'en'
    );

    expect(rows.map((row) => row.labelKey)).not.toEqual(
      expect.arrayContaining([
        'pages.orders.work_contract.facts.services',
        'pages.orders.work_contract.facts.packages',
        'pages.orders.work_contract.facts.extras',
      ])
    );
    expect(rows).toHaveLength(5);
  });

  it('renders no rows without facts', () => {
    expect(buildWorkContractFactRows(undefined, 'en')).toEqual([]);
  });
});

describe('languageDisplayName', () => {
  it('names the accepted language in the UI language', () => {
    expect(languageDisplayName('cs', 'en')).toBe('Czech');
    expect(languageDisplayName('en', 'cs')).toBe('Angličtina');
  });

  it('falls back to the upper-cased code for an unknown language', () => {
    expect(languageDisplayName('zz', 'en')).toBe('ZZ');
  });
});

// Every member of a generated command is optional, so a dropped assignment type-checks.
// The serialized body is what the server reads (ADR-0031).
describe('command bodies', () => {
  it('builds the take with the order id and the echoed text id', () => {
    const command = buildTakeOrderCommand(ORDER_ID, TEXT_ID);

    expect(command).toBeInstanceOf(TakeOrderCommand);
    expect(command.toJSON()).toEqual({
      orderId: ORDER_ID,
      acceptedWorkContractTextId: TEXT_ID,
    });
  });

  it('builds the standalone acceptance with the order id and the echoed text id', () => {
    const command = buildAcceptWorkContractCommand(ORDER_ID, TEXT_ID);

    expect(command).toBeInstanceOf(AcceptWorkContractCommand);
    expect(command.toJSON()).toEqual({
      orderId: ORDER_ID,
      acceptedWorkContractTextId: TEXT_ID,
    });
  });
});
