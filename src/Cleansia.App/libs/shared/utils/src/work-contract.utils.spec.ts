import {
  buildWorkContractFactRows,
  formatCleaningWindow,
  languageDisplayName,
  WorkContractFactsView,
} from './work-contract.utils';

const FACTS_KEY = 'pages.some_page.work_contract.facts';

function facts(overrides: Partial<WorkContractFactsView> = {}): WorkContractFactsView {
  return {
    orderNumber: 'CLS-42',
    cleaningDateTimeUtc: new Date('2026-10-03T08:30:00Z'),
    estimatedMinutes: 150,
    totalPrice: 1250,
    currencyCode: 'CZK',
    locationApproximate: 'Praha 6, 160 00',
    rooms: 3,
    bathrooms: 1,
    services: [{ id: 's1', name: 'Window cleaning' }],
    packages: [{ id: 'p1', name: 'Move-out bundle' }],
    extraSlugs: ['fridge', 'oven'],
    ...overrides,
  };
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
  it('lists the frozen job facts in the order the contract names them, under the given key', () => {
    const rows = buildWorkContractFactRows(facts(), 'en', FACTS_KEY);

    expect(rows.map((row) => row.labelKey)).toEqual([
      `${FACTS_KEY}.order_number`,
      `${FACTS_KEY}.window`,
      `${FACTS_KEY}.price`,
      `${FACTS_KEY}.location`,
      `${FACTS_KEY}.rooms_bathrooms`,
      `${FACTS_KEY}.services`,
      `${FACTS_KEY}.packages`,
      `${FACTS_KEY}.extras`,
    ]);
    expect(rows[0].value).toBe('CLS-42');
    expect(rows[1].value).toBe(formatCleaningWindow(new Date('2026-10-03T08:30:00Z'), 150));
    expect(rows[2].value).toBe('CZK 1,250');
    expect(rows[3].value).toBe('Praha 6, 160 00');
    expect(rows[4].value).toBe('3 / 1');
    expect(rows[5].value).toBe('Window cleaning');
    expect(rows[6].value).toBe('Move-out bundle');
    expect(rows[7].value).toBe('fridge, oven');
  });

  it('formats the price in the UI language', () => {
    const rows = buildWorkContractFactRows(facts(), 'cs', FACTS_KEY);

    expect(rows[2].value).toBe('1 250 Kč');
  });

  it('drops the scope rows the job does not have', () => {
    const rows = buildWorkContractFactRows(
      facts({ services: [], packages: [], extraSlugs: [] }),
      'en',
      FACTS_KEY
    );

    expect(rows).toHaveLength(5);
  });

  it('skips a line with no name rather than printing an empty one', () => {
    const rows = buildWorkContractFactRows(
      facts({ services: [{ id: 's1', name: 'Window cleaning' }, { id: 's2', name: undefined }] }),
      'en',
      FACTS_KEY
    );

    expect(rows[5].value).toBe('Window cleaning');
  });

  it('renders no rows without facts', () => {
    expect(buildWorkContractFactRows(undefined, 'en', FACTS_KEY)).toEqual([]);
  });
});

describe('languageDisplayName', () => {
  it('names the language in the UI language', () => {
    expect(languageDisplayName('cs', 'en')).toBe('Czech');
    expect(languageDisplayName('en', 'cs')).toBe('Angličtina');
  });

  it('falls back to the upper-cased code for an unknown language', () => {
    expect(languageDisplayName('zz', 'en')).toBe('ZZ');
  });
});
