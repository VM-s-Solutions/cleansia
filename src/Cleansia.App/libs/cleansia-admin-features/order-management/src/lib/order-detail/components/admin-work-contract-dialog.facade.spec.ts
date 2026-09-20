import { TestBed } from '@angular/core/testing';
import { AdminClient, AdminLegalDocumentDto, WorkContractDto } from '@cleansia/admin-services';
import { TranslateService } from '@ngx-translate/core';
import { DynamicDialogRef } from 'primeng/dynamicdialog';
import { of, Subject, throwError } from 'rxjs';
import { AdminWorkContractDialogFacade } from './admin-work-contract-dialog.facade';
import { WORK_CONTRACT_HASH_ROW } from './admin-work-contract-dialog.models';

const ACCEPTANCE_ID = 'acc-1';
const DOCUMENT_ID = 'doc-1';
const HASH = 'f'.repeat(64);

function contract(overrides: Record<string, unknown> = {}): WorkContractDto {
  return WorkContractDto.fromJS({
    legalDocumentTextId: 'text-en-1',
    legalDocumentId: DOCUMENT_ID,
    version: '2026-09-20',
    effectiveFrom: '2026-09-20',
    language: 'en',
    title: 'Contract for work',
    contentHtml: '<h2>1. Parties</h2><p>The customer and the cleaner.</p>',
    facts: {
      orderNumber: 'CLS-42',
      cleaningDateTimeUtc: '2026-10-03T08:30:00Z',
      estimatedMinutes: 120,
      totalPrice: 1250,
      currencyCode: 'CZK',
      locationApproximate: 'Praha 6',
      countryId: 'CZ',
      rooms: 3,
      bathrooms: 1,
      services: [],
      packages: [],
      extraSlugs: [],
    },
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

function document(overrides: Record<string, unknown> = {}): AdminLegalDocumentDto {
  return AdminLegalDocumentDto.fromJS({
    id: DOCUMENT_ID,
    language: 'cs',
    contentHash: HASH,
    ...overrides,
  });
}

describe('AdminWorkContractDialogFacade', () => {
  let getWorkContract: jest.Mock;
  let getDocument: jest.Mock;
  let dialogRef: { close: jest.Mock };

  const createFacade = (): AdminWorkContractDialogFacade => {
    TestBed.configureTestingModule({
      providers: [
        AdminWorkContractDialogFacade,
        {
          provide: AdminClient,
          useValue: { adminOrderClient: { getWorkContract }, adminLegalClient: { getDocument } },
        },
        { provide: DynamicDialogRef, useValue: dialogRef },
        { provide: TranslateService, useValue: { currentLang: 'en', instant: (k: string) => k } },
      ],
    });
    return TestBed.inject(AdminWorkContractDialogFacade);
  };

  beforeEach(() => {
    TestBed.resetTestingModule();
    getWorkContract = jest.fn().mockReturnValue(of(contract()));
    getDocument = jest.fn().mockReturnValue(of(document()));
    dialogRef = { close: jest.fn() };
  });

  it('reads the accepted contract in the UI language and the accepted text row for its hash', () => {
    const facade = createFacade();

    facade.load(ACCEPTANCE_ID);

    expect(getWorkContract).toHaveBeenCalledWith(ACCEPTANCE_ID, 'en');
    // The hash the dispute bundle cites is the accepted text's, which is the Czech row here — not
    // the English one the dialog renders.
    expect(getDocument).toHaveBeenCalledWith(DOCUMENT_ID, 'cs');
    expect(facade.contract()?.legalDocumentTextId).toBe('text-en-1');
    expect(facade.acceptedTextHash()).toBe(HASH);
    expect(facade.loading()).toBe(false);
    expect(facade.loadFailed()).toBe(false);
    expect(facade.factRows().map((row) => row.value)).toContain('CLS-42');
    expect(facade.acceptanceRows().find((row) => row.labelKey === WORK_CONTRACT_HASH_ROW)?.value).toBe(HASH);
  });

  it('names the rendered language beside the accepted one only when they differ', () => {
    const facade = createFacade();

    facade.load(ACCEPTANCE_ID);
    expect(facade.renderedLanguageNotice()).toEqual({ language: 'English', accepted: 'Czech' });

    getWorkContract.mockReturnValue(
      of(contract({ acceptance: { acceptedOn: '2026-09-21T10:00:00Z', acceptedLanguage: 'en' } }))
    );
    getDocument.mockReturnValue(of(document({ language: 'en' })));
    facade.load(ACCEPTANCE_ID);
    expect(facade.renderedLanguageNotice()).toBeNull();
  });

  it('holds the loading state while both reads are in flight', () => {
    const pending = new Subject<WorkContractDto>();
    getWorkContract.mockReturnValue(pending);
    const facade = createFacade();

    facade.load(ACCEPTANCE_ID);
    expect(facade.loading()).toBe(true);
    expect(facade.contract()).toBeNull();

    pending.next(contract());
    pending.complete();
    expect(facade.loading()).toBe(false);
    expect(facade.contract()).not.toBeNull();
  });

  // The hash is a second read; without it the contract is still the record, so the dialog shows it
  // and says the hash could not be read rather than failing the whole dialog.
  it('shows the contract without a hash when the document read fails', () => {
    getDocument.mockReturnValue(throwError(() => new Error('down')));
    const facade = createFacade();

    facade.load(ACCEPTANCE_ID);

    expect(facade.contract()).not.toBeNull();
    expect(facade.acceptedTextHash()).toBeNull();
    expect(facade.loadFailed()).toBe(false);
    expect(facade.acceptanceRows().find((row) => row.labelKey === WORK_CONTRACT_HASH_ROW)?.valueKey).toBe(
      'pages.order_detail.work_contract.dialog.acceptance.hash_unavailable'
    );
  });

  // The admin read falls back to another language when the requested one is missing; a hash of a
  // text the cleaner did not accept must not be labelled as theirs.
  it('shows no hash when the document read answers another language than the accepted one', () => {
    getDocument.mockReturnValue(of(document({ language: 'en' })));
    const facade = createFacade();

    facade.load(ACCEPTANCE_ID);

    expect(facade.acceptedTextHash()).toBeNull();
  });

  it('enters the error state when the contract read fails, and retries the same acceptance', () => {
    getWorkContract.mockReturnValueOnce(throwError(() => new Error('down')));
    const facade = createFacade();

    facade.load(ACCEPTANCE_ID);
    expect(facade.loadFailed()).toBe(true);
    expect(facade.contract()).toBeNull();
    expect(facade.loading()).toBe(false);
    expect(getDocument).not.toHaveBeenCalled();

    facade.retry();
    expect(getWorkContract).toHaveBeenLastCalledWith(ACCEPTANCE_ID, 'en');
    expect(facade.loadFailed()).toBe(false);
    expect(facade.contract()).not.toBeNull();
  });

  it('closes through the dialog reference', () => {
    const facade = createFacade();

    facade.close();

    expect(dialogRef.close).toHaveBeenCalledTimes(1);
  });
});
