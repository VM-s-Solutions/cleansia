import { TestBed } from '@angular/core/testing';
import {
  AdminClient,
  AdminLegalDocumentDto,
  LegalDocumentAudience,
  LegalDocumentTextSummaryDto,
  LegalDocumentType,
  LegalDocumentVersionDto,
} from '@cleansia/admin-services';
import { TranslateService } from '@ngx-translate/core';
import { of, Subject, throwError } from 'rxjs';
import { LegalDocumentsFacade } from './legal-documents.facade';

describe('LegalDocumentsFacade', () => {
  let facade: LegalDocumentsFacade;
  let getVersionsMock: jest.Mock;
  let getDocumentMock: jest.Mock;
  let translate: { currentLang: string; instant: (key: string) => string };

  const text = (language: string) =>
    LegalDocumentTextSummaryDto.fromJS({ language, title: language.toUpperCase(), contentHash: `hash-${language}` });

  const terms = LegalDocumentVersionDto.fromJS({
    id: 'doc-terms',
    audience: LegalDocumentAudience.Customer,
    type: LegalDocumentType.TermsOfService,
    effectiveFrom: '2026-09-14',
    version: '2026-09-14',
    isInForce: true,
    texts: [text('en'), text('cs'), text('sk')],
  });

  const privacy = LegalDocumentVersionDto.fromJS({
    id: 'doc-privacy',
    audience: LegalDocumentAudience.Customer,
    type: LegalDocumentType.PrivacyPolicy,
    effectiveFrom: '2026-09-14',
    version: '2026-09-14',
    isInForce: true,
    texts: [text('en')],
  });

  const documentIn = (language: string) =>
    AdminLegalDocumentDto.fromJS({
      id: 'doc-terms',
      language,
      title: language.toUpperCase(),
      contentHtml: `<h2>${language}</h2>`,
      contentHash: `hash-${language}`,
    });

  beforeEach(() => {
    getVersionsMock = jest.fn().mockReturnValue(of([terms, privacy]));
    getDocumentMock = jest.fn().mockImplementation((_id: string, language: string) => of(documentIn(language)));
    translate = { currentLang: 'cs', instant: (key: string) => key };

    TestBed.configureTestingModule({
      providers: [
        LegalDocumentsFacade,
        {
          provide: AdminClient,
          useValue: {
            adminLegalClient: { getVersions: getVersionsMock, getDocument: getDocumentMock },
          },
        },
        { provide: TranslateService, useValue: translate },
      ],
    });

    facade = TestBed.inject(LegalDocumentsFacade);
  });

  describe('loadVersions', () => {
    it('loads every version unfiltered and settles the loading flags', () => {
      facade.loadVersions();

      expect(getVersionsMock).toHaveBeenCalledWith(undefined, undefined, undefined);
      expect(facade.versions()).toEqual([terms, privacy]);
      expect(facade.loading()).toBe(false);
      expect(facade.initialLoading()).toBe(false);
      expect(facade.hasError()).toBe(false);
    });

    // Seeded with `of(null)`, not a plausible array: the generated client answers a non-array 200
    // and a 204 with NULL. → service-form.facade.spec.ts
    it('leaves the version list an empty array when the client answers null', () => {
      getVersionsMock.mockReturnValue(of(null));

      facade.loadVersions();

      expect(facade.versions()).toEqual([]);
      expect(facade.initialLoading()).toBe(false);
      expect(facade.hasError()).toBe(false);
    });

    it('settles the error state and stops loading when the read fails', () => {
      getVersionsMock.mockReturnValue(throwError(() => new Error('boom')));

      facade.loadVersions();

      expect(facade.hasError()).toBe(true);
      expect(facade.versions()).toEqual([]);
      expect(facade.loading()).toBe(false);
      expect(facade.initialLoading()).toBe(false);
    });

    it('clears a previous error on the next successful read', () => {
      getVersionsMock.mockReturnValueOnce(throwError(() => new Error('boom')));
      facade.loadVersions();
      expect(facade.hasError()).toBe(true);

      facade.loadVersions();

      expect(facade.hasError()).toBe(false);
      expect(facade.versions()).toEqual([terms, privacy]);
    });
  });

  describe('preview', () => {
    it('selects the version, opens it in the current UI language and loads that text', () => {
      facade.preview(terms);

      expect(facade.selectedVersion()).toBe(terms);
      expect(facade.language()).toBe('cs');
      expect(getDocumentMock).toHaveBeenCalledWith('doc-terms', 'cs');
      expect(facade.document()?.contentHtml).toBe('<h2>cs</h2>');
      expect(facade.documentLoading()).toBe(false);
      expect(facade.documentError()).toBe(false);
    });

    it('falls back to English when the version has no text in the UI language', () => {
      facade.preview(privacy);

      expect(facade.language()).toBe('en');
      expect(getDocumentMock).toHaveBeenCalledWith('doc-privacy', 'en');
    });

    it('loads nothing for a version without texts', () => {
      facade.preview(LegalDocumentVersionDto.fromJS({ ...terms, texts: [] }));

      expect(facade.language()).toBeNull();
      expect(facade.document()).toBeNull();
      expect(getDocumentMock).not.toHaveBeenCalled();
    });

    it('settles the document error state, clears the text and stops loading on failure', () => {
      getDocumentMock.mockReturnValue(throwError(() => new Error('boom')));

      facade.preview(terms);

      expect(facade.documentError()).toBe(true);
      expect(facade.document()).toBeNull();
      expect(facade.documentLoading()).toBe(false);
      expect(facade.selectedVersion()).toBe(terms);
    });

    it('clears the previous text while the next one is loading', () => {
      const pending = new Subject<AdminLegalDocumentDto>();
      facade.preview(terms);
      getDocumentMock.mockReturnValue(pending);

      facade.showLanguage(text('sk'));

      expect(facade.document()).toBeNull();
      expect(facade.documentLoading()).toBe(true);
      pending.next(documentIn('sk'));
      pending.complete();
      expect(facade.document()?.language).toBe('sk');
      expect(facade.documentLoading()).toBe(false);
    });

    it('drops a slow earlier response once a newer language was requested', () => {
      const slowCs = new Subject<AdminLegalDocumentDto>();
      const fastSk = new Subject<AdminLegalDocumentDto>();
      getDocumentMock.mockReturnValueOnce(slowCs).mockReturnValueOnce(fastSk);

      facade.preview(terms);
      facade.showLanguage(text('sk'));
      fastSk.next(documentIn('sk'));
      fastSk.complete();
      slowCs.next(documentIn('cs'));
      slowCs.complete();

      expect(facade.language()).toBe('sk');
      expect(facade.document()?.language).toBe('sk');
    });
  });

  describe('showLanguage', () => {
    it('switches the previewed language of the selected version', () => {
      facade.preview(terms);

      facade.showLanguage(text('en'));

      expect(facade.language()).toBe('en');
      expect(getDocumentMock).toHaveBeenLastCalledWith('doc-terms', 'en');
      expect(facade.document()?.language).toBe('en');
    });

    it('does nothing when no version is selected', () => {
      facade.showLanguage(text('en'));

      expect(getDocumentMock).not.toHaveBeenCalled();
      expect(facade.language()).toBeNull();
    });
  });

  describe('retryText', () => {
    it('re-requests the language whose read failed', () => {
      getDocumentMock.mockReturnValueOnce(throwError(() => new Error('boom')));
      facade.preview(terms);
      expect(facade.documentError()).toBe(true);

      facade.retryText();

      expect(getDocumentMock).toHaveBeenLastCalledWith('doc-terms', 'cs');
      expect(facade.documentError()).toBe(false);
      expect(facade.document()?.language).toBe('cs');
    });

    it('does nothing without a selected version', () => {
      facade.retryText();

      expect(getDocumentMock).not.toHaveBeenCalled();
    });
  });

  describe('closePreview', () => {
    it('clears the selection, the language and the text', () => {
      facade.preview(terms);

      facade.closePreview();

      expect(facade.selectedVersion()).toBeNull();
      expect(facade.language()).toBeNull();
      expect(facade.document()).toBeNull();
      expect(facade.documentError()).toBe(false);
    });
  });
});
