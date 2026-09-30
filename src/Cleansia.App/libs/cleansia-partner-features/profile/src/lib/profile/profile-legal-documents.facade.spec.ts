import { TestBed } from '@angular/core/testing';
import {
  AcceptLegalDocumentCommand,
  AcceptLegalDocumentResponse,
  CleanerLegalDocumentDto,
  LegalDocumentType,
  PartnerClient,
} from '@cleansia/partner-services';
import { SnackbarService } from '@cleansia/services';
import { TranslateService } from '@ngx-translate/core';
import { Subject, of, throwError } from 'rxjs';
import { ProfileLegalDocumentsFacade } from './profile-legal-documents.facade';

const TEXT_ID = 'text-framework-cs-1';
const NEWER_TEXT_ID = 'text-framework-cs-2';

function legalDocument(overrides: Record<string, unknown> = {}): CleanerLegalDocumentDto {
  return CleanerLegalDocumentDto.fromJS({
    type: LegalDocumentType.CleanerFrameworkContract,
    legalDocumentId: 'doc-1',
    legalDocumentTextId: TEXT_ID,
    version: '2026-10-01',
    effectiveFrom: '2026-10-01',
    language: 'cs',
    title: 'Rámcová smlouva',
    contentHtml: '<h2>1. Strany</h2>',
    contentHash: 'hash',
    isAccepted: false,
    acceptedVersion: null,
    acceptedAt: null,
    ...overrides,
  });
}

// The shape CleansiaApiController.HandleFailure puts on the wire for a validation refusal.
function refusal(code: string): unknown {
  return { detail: 'A validation problem occurred.', errors: { AcceptedTextId: code } };
}

describe('ProfileLegalDocumentsFacade', () => {
  let employeeClient: { getMyLegalDocuments: jest.Mock; acceptLegalDocument: jest.Mock };
  let snackbar: {
    showSuccess: jest.Mock; showSuccessTranslated: jest.Mock;
    showError: jest.Mock; showErrorTranslated: jest.Mock;
    showApiError: jest.Mock;
  };

  const createFacade = (): ProfileLegalDocumentsFacade => {
    TestBed.configureTestingModule({
      providers: [
        ProfileLegalDocumentsFacade,
        { provide: PartnerClient, useValue: { employeeClient } },
        { provide: SnackbarService, useValue: snackbar },
        {
          provide: TranslateService,
          useValue: { instant: (key: string) => key, currentLang: 'cs' },
        },
      ],
    });

    return TestBed.inject(ProfileLegalDocumentsFacade);
  };

  const sentCommand = (): AcceptLegalDocumentCommand =>
    employeeClient.acceptLegalDocument.mock.calls[0][0] as AcceptLegalDocumentCommand;

  beforeEach(() => {
    TestBed.resetTestingModule();
    employeeClient = {
      getMyLegalDocuments: jest.fn().mockReturnValue(of([legalDocument()])),
      acceptLegalDocument: jest.fn().mockReturnValue(
        of(
          AcceptLegalDocumentResponse.fromJS({
            type: LegalDocumentType.CleanerFrameworkContract,
            version: '2026-10-01',
          })
        )
      ),
    };
    snackbar = {
      showSuccess: jest.fn(), showSuccessTranslated: jest.fn(),
      showError: jest.fn(), showErrorTranslated: jest.fn(),
      showApiError: jest.fn(),
    };
  });

  describe('loading', () => {
    it('shows the loading state while the first read is in flight', () => {
      const pending = new Subject<CleanerLegalDocumentDto[]>();
      employeeClient.getMyLegalDocuments.mockReturnValue(pending);
      const facade = createFacade();

      facade.load();

      expect(facade.loading()).toBe(true);
      expect(facade.loaded()).toBe(false);
      expect(facade.visible()).toBe(true);
    });

    it('reads the documents in force in the UI language', () => {
      const facade = createFacade();

      facade.load();

      expect(employeeClient.getMyLegalDocuments).toHaveBeenCalledWith('cs');
      expect(facade.documents().map((d) => d.legalDocumentTextId)).toEqual([TEXT_ID]);
      expect(facade.loading()).toBe(false);
      expect(facade.loaded()).toBe(true);
      expect(facade.loadFailed()).toBe(false);
      expect(facade.visible()).toBe(true);
    });

    it('hides the section while no cleaner document is in force', () => {
      employeeClient.getMyLegalDocuments.mockReturnValue(of([]));
      const facade = createFacade();

      facade.load();

      expect(facade.loaded()).toBe(true);
      expect(facade.visible()).toBe(false);
    });

    it('holds an empty list, not null, when the read answers a null body', () => {
      employeeClient.getMyLegalDocuments.mockReturnValue(of(null));
      const facade = createFacade();

      facade.load();

      expect(facade.documents()).toEqual([]);
      expect(facade.visible()).toBe(false);
    });

    it('shows the error state when the first read fails, and a retry recovers', () => {
      employeeClient.getMyLegalDocuments.mockReturnValueOnce(throwError(() => new Error('down')));
      const facade = createFacade();

      facade.load();

      expect(facade.loadFailed()).toBe(true);
      expect(facade.loading()).toBe(false);
      expect(facade.visible()).toBe(true);

      facade.retry();

      expect(facade.loadFailed()).toBe(false);
      expect(facade.documents()).toHaveLength(1);
    });

    it('keeps the list on screen when a later re-read fails', () => {
      const facade = createFacade();
      facade.load();
      employeeClient.getMyLegalDocuments.mockReturnValue(throwError(() => new Error('down')));

      facade.load();

      expect(facade.loadFailed()).toBe(false);
      expect(facade.documents()).toHaveLength(1);
    });
  });

  describe('accepting', () => {
    it('echoes the text id it rendered, confirms and re-reads the list', () => {
      const facade = createFacade();
      facade.load();
      employeeClient.getMyLegalDocuments.mockReturnValue(
        of([legalDocument({ isAccepted: true, acceptedVersion: '2026-10-01', acceptedAt: '2026-10-02T09:00:00Z' })])
      );

      facade.accept(facade.documents()[0]);

      const command = sentCommand();
      expect(command).toBeInstanceOf(AcceptLegalDocumentCommand);
      expect(command.toJSON()).toEqual({ acceptedTextId: TEXT_ID });
      expect(snackbar.showSuccessTranslated).toHaveBeenCalledWith(
        'global.messages.profile.legal_document_accepted'
      );
      expect(employeeClient.getMyLegalDocuments).toHaveBeenCalledTimes(2);
      expect(facade.documents()[0].isAccepted).toBe(true);
      expect(facade.accepting()).toBe(false);
      expect(facade.acceptingType()).toBeNull();
    });

    it('marks the document being accepted and refuses a second accept while in flight', () => {
      const pending = new Subject<AcceptLegalDocumentResponse>();
      employeeClient.acceptLegalDocument.mockReturnValue(pending);
      const facade = createFacade();
      facade.load();

      facade.accept(facade.documents()[0]);
      facade.accept(facade.documents()[0]);

      expect(facade.acceptingType()).toBe(LegalDocumentType.CleanerFrameworkContract);
      expect(facade.accepting()).toBe(true);
      expect(employeeClient.acceptLegalDocument).toHaveBeenCalledTimes(1);
    });

    it('sends nothing for a document without a text id', () => {
      const facade = createFacade();

      facade.accept(legalDocument({ legalDocumentTextId: undefined }));

      expect(employeeClient.acceptLegalDocument).not.toHaveBeenCalled();
    });

    it('re-reads the list when the text shown is no longer in force, so the newer one is accepted next', () => {
      employeeClient.acceptLegalDocument.mockReturnValue(
        throwError(() => refusal('legal.document_not_in_force'))
      );
      const facade = createFacade();
      facade.load();
      employeeClient.getMyLegalDocuments.mockReturnValue(
        of([legalDocument({ legalDocumentTextId: NEWER_TEXT_ID, version: '2026-11-01' })])
      );

      facade.accept(facade.documents()[0]);

      expect(snackbar.showSuccessTranslated).not.toHaveBeenCalled();
      expect(facade.documents()[0].legalDocumentTextId).toBe(NEWER_TEXT_ID);
      expect(facade.accepting()).toBe(false);
    });

    it('leaves any other refusal to the interceptor toast and keeps the list as it was', () => {
      employeeClient.acceptLegalDocument.mockReturnValue(
        throwError(() => refusal('general.not_found'))
      );
      const facade = createFacade();
      facade.load();

      facade.accept(facade.documents()[0]);

      expect(snackbar.showSuccessTranslated).not.toHaveBeenCalled();
      expect(employeeClient.getMyLegalDocuments).toHaveBeenCalledTimes(1);
      expect(facade.accepting()).toBe(false);
    });
  });
});
