import { WorkContractFacts } from '@cleansia/customer-services';
import {
  buildWorkContractFactRows,
  formatCleaningWindow,
  formatDateTime,
  languageDisplayName,
} from './work-contract-page.models';

const FACTS = WorkContractFacts.fromJS({
  orderNumber: 'ORD-2026-0042',
  cleaningDateTimeUtc: '2026-09-25T08:00:00Z',
  estimatedMinutes: 150,
  totalPrice: 1550,
  currencyCode: 'CZK',
  locationApproximate: 'Praha · 120',
  countryId: 'cze-id',
  rooms: 3,
  bathrooms: 1,
  services: [{ id: 's1', name: 'Standard cleaning' }, { id: 's2', name: 'Windows' }],
  packages: [{ id: 'p1', name: 'Spring package' }],
  extraSlugs: ['fridge', 'oven'],
});

describe('formatDateTime', () => {
  it('reads as the date and the time in the given locale', () => {
    expect(formatDateTime(new Date(2026, 8, 21, 10, 15), 'cs-CZ')).toBe('21. 09. 2026 10:15');
    expect(formatDateTime(new Date(2026, 8, 21, 10, 15), 'en-US')).toBe('09/21/2026, 10:15 AM');
  });
});

describe('formatCleaningWindow', () => {
  const start = new Date(2026, 8, 25, 8, 0);

  it('reads as the start instant and the end time the estimate produces', () => {
    expect(formatCleaningWindow(start, 150, 'cs-CZ')).toBe('25. 09. 2026 8:00 – 10:30');
  });

  it('reads as the start alone when there is no estimate', () => {
    expect(formatCleaningWindow(start, 0, 'cs-CZ')).toBe('25. 09. 2026 8:00');
  });

  it('is empty without a start', () => {
    expect(formatCleaningWindow(undefined, 150, 'cs-CZ')).toBe('');
  });
});

describe('buildWorkContractFactRows', () => {
  it('lists the frozen facts in the order the contract names them, priced in the frozen currency', () => {
    const rows = buildWorkContractFactRows(FACTS, 'en');

    expect(rows.map((row) => row.labelKey)).toEqual([
      'pages.order_contract.facts.order_number',
      'pages.order_contract.facts.window',
      'pages.order_contract.facts.price',
      'pages.order_contract.facts.location',
      'pages.order_contract.facts.rooms_bathrooms',
      'pages.order_contract.facts.services',
      'pages.order_contract.facts.packages',
      'pages.order_contract.facts.extras',
    ]);
    expect(rows[0].value).toBe('ORD-2026-0042');
    expect(rows[2].value).toBe('CZK 1,550');
    expect(rows[3].value).toBe('Praha · 120');
    expect(rows[4].value).toBe('3 / 1');
    expect(rows[5].value).toBe('Standard cleaning, Windows');
    expect(rows[6].value).toBe('Spring package');
    expect(rows[7].value).toBe('fridge, oven');
  });

  it('formats the price in the UI language, not the market', () => {
    const rows = buildWorkContractFactRows(FACTS, 'cs');

    expect(rows[2].value).toBe('1 550 Kč');
  });

  it('omits the scope rows the job does not carry', () => {
    const bare = WorkContractFacts.fromJS({ ...FACTS.toJSON(), services: [], packages: [], extraSlugs: [] });

    const rows = buildWorkContractFactRows(bare, 'en');

    expect(rows.map((row) => row.labelKey)).toEqual([
      'pages.order_contract.facts.order_number',
      'pages.order_contract.facts.window',
      'pages.order_contract.facts.price',
      'pages.order_contract.facts.location',
      'pages.order_contract.facts.rooms_bathrooms',
    ]);
  });

  it('lists nothing without facts', () => {
    expect(buildWorkContractFactRows(undefined, 'en')).toEqual([]);
  });
});

describe('languageDisplayName', () => {
  it('names the accepted language in the UI language', () => {
    expect(languageDisplayName('cs', 'en')).toBe('Czech');
    expect(languageDisplayName('en', 'cs')).toBe('Angličtina');
  });

  it('falls back to the upper-cased code when the tag is unknown', () => {
    expect(languageDisplayName('zz', 'en')).toBe('ZZ');
  });
});
