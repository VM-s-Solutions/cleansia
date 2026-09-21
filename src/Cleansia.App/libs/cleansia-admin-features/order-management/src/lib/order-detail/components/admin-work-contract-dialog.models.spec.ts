import { WorkContractDto } from '@cleansia/admin-services';
import { formatDate } from '@cleansia/utils';
import {
  buildWorkContractAcceptanceRows,
  WORK_CONTRACT_HASH_ROW,
} from './admin-work-contract-dialog.models';

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

// The acceptance block is the dispute answer: when, which version, which language, and the SHA-256
// of the exact text row — the admin document read's hash (ADR-0063 D8), never a client computation.
describe('buildWorkContractAcceptanceRows', () => {
  it('lists the instant, the version, the accepted language and the hash of the accepted text', () => {
    const rows = buildWorkContractAcceptanceRows(contract(), 'a'.repeat(64), 'en', true);

    expect(rows.map((row) => row.labelKey)).toEqual([
      'pages.order_detail.work_contract.dialog.acceptance.accepted_on',
      'pages.order_detail.work_contract.dialog.acceptance.version',
      'pages.order_detail.work_contract.dialog.acceptance.language',
      WORK_CONTRACT_HASH_ROW,
    ]);
    expect(rows[0].value).toBe(formatDate(new Date('2026-09-21T10:00:00Z'), 'en', 'dateTime'));
    expect(rows[1].value).toBe('2026-09-20');
    expect(rows[2].value).toBe('Czech');
    expect(rows[3].value).toBe('a'.repeat(64));
  });

  it('says so when the hash could not be read rather than dropping the row', () => {
    const rows = buildWorkContractAcceptanceRows(contract(), null, 'en', true);

    expect(rows[3]).toEqual({
      labelKey: WORK_CONTRACT_HASH_ROW,
      value: '',
      valueKey: 'pages.order_detail.work_contract.dialog.acceptance.hash_unavailable',
    });
  });

  // The document read behind the hash is Administrator-only; a Support, Accountant or Operations
  // session never makes it, and the row must say that rather than pass a permission gap off as a
  // failed read.
  it('says the hash is not shown for the role when the session cannot read the document', () => {
    const rows = buildWorkContractAcceptanceRows(contract(), null, 'en', false);

    expect(rows[3]).toEqual({
      labelKey: WORK_CONTRACT_HASH_ROW,
      value: '',
      valueKey: 'pages.order_detail.work_contract.dialog.acceptance.hash_not_visible',
    });
  });

  it('renders no rows for a contract with no acceptance', () => {
    expect(buildWorkContractAcceptanceRows(contract({ acceptance: undefined }), 'x', 'en', true)).toEqual([]);
    expect(buildWorkContractAcceptanceRows(null, 'x', 'en', true)).toEqual([]);
  });
});
