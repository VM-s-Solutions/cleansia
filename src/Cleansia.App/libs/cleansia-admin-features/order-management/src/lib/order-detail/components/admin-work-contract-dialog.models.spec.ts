import { WorkContractDto, WorkContractFacts } from '@cleansia/admin-services';
import {
  buildWorkContractAcceptanceRows,
  buildWorkContractFactRows,
  formatCleaningWindow,
  languageDisplayName,
  WORK_CONTRACT_HASH_ROW,
} from './admin-work-contract-dialog.models';

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

function contract(overrides: Record<string, unknown> = {}): WorkContractDto {
  return WorkContractDto.fromJS({
    legalDocumentTextId: 'text-cs-1',
    legalDocumentId: 'doc-1',
    version: '2026-09-20',
    effectiveFrom: '2026-09-20',
    language: 'en',
    title: 'Contract for work',
    contentHtml: '<p>Text</p>',
    acceptance: {
      acceptedOn: '2026-09-21T10:00:00Z',
      documentVersion: '2026-09-20',
      acceptedLanguage: 'cs',
      orderEmployeeId: 'seat-1',
      employeeId: 'emp-1',
    },
    ...overrides,
  });
}

describe('formatCleaningWindow', () => {
  it('renders the start and the end the estimate implies, on one line', () => {
    const start = new Date(2026, 9, 3, 10, 30);

    expect(formatCleaningWindow(start, 150)).toBe('03/10/2026, 10:30 – 13:00');
  });

  it('renders only the start when the estimate is missing', () => {
    const start = new Date(2026, 9, 3, 10, 30);

    expect(formatCleaningWindow(start, 0)).toBe('03/10/2026, 10:30');
  });

  it('renders nothing without a start', () => {
    expect(formatCleaningWindow(undefined, 90)).toBe('');
  });
});

describe('buildWorkContractFactRows', () => {
  it('lists the frozen job facts in the order the contract names them', () => {
    const rows = buildWorkContractFactRows(facts(), 'en');

    expect(rows.map((row) => row.labelKey)).toEqual([
      'pages.order_detail.work_contract.dialog.facts.order_number',
      'pages.order_detail.work_contract.dialog.facts.window',
      'pages.order_detail.work_contract.dialog.facts.price',
      'pages.order_detail.work_contract.dialog.facts.location',
      'pages.order_detail.work_contract.dialog.facts.rooms_bathrooms',
      'pages.order_detail.work_contract.dialog.facts.services',
      'pages.order_detail.work_contract.dialog.facts.packages',
      'pages.order_detail.work_contract.dialog.facts.extras',
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
    const rows = buildWorkContractFactRows(facts({ services: [], packages: [], extraSlugs: [] }), 'en');

    expect(rows).toHaveLength(5);
  });

  it('renders no rows without facts', () => {
    expect(buildWorkContractFactRows(undefined, 'en')).toEqual([]);
  });
});

// The acceptance block is the dispute answer: when, which version, which language, and the SHA-256
// of the exact text row — the admin document read's hash (ADR-0063 D8), never a client computation.
describe('buildWorkContractAcceptanceRows', () => {
  it('lists the instant, the version, the accepted language and the hash of the accepted text', () => {
    const rows = buildWorkContractAcceptanceRows(contract(), 'a'.repeat(64), 'en');

    expect(rows.map((row) => row.labelKey)).toEqual([
      'pages.order_detail.work_contract.dialog.acceptance.accepted_on',
      'pages.order_detail.work_contract.dialog.acceptance.version',
      'pages.order_detail.work_contract.dialog.acceptance.language',
      WORK_CONTRACT_HASH_ROW,
    ]);
    expect(rows[0].value).toBe(new Date('2026-09-21T10:00:00Z').toLocaleString('en-GB'));
    expect(rows[1].value).toBe('2026-09-20');
    expect(rows[2].value).toBe('Czech');
    expect(rows[3].value).toBe('a'.repeat(64));
  });

  it('says so when the hash could not be read rather than dropping the row', () => {
    const rows = buildWorkContractAcceptanceRows(contract(), null, 'en');

    expect(rows[3]).toEqual({
      labelKey: WORK_CONTRACT_HASH_ROW,
      value: '',
      valueKey: 'pages.order_detail.work_contract.dialog.acceptance.hash_unavailable',
    });
  });

  it('renders no rows for a contract with no acceptance', () => {
    expect(buildWorkContractAcceptanceRows(contract({ acceptance: undefined }), 'x', 'en')).toEqual([]);
    expect(buildWorkContractAcceptanceRows(null, 'x', 'en')).toEqual([]);
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
