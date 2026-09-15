import { TestBed } from '@angular/core/testing';
import { CustomerClient, LegalDocumentDto, LegalDocumentType } from '@cleansia/customer-services';
import { selectMarketCountryId } from '@cleansia/customer-stores';
import { MockStore, provideMockStore } from '@ngrx/store/testing';
import { TranslateService } from '@ngx-translate/core';
import { Subject, of, throwError } from 'rxjs';
import { LegalDocumentFacade } from './legal-document.facade';

const TERMS = LegalDocumentDto.fromJS({
  type: LegalDocumentType.TermsOfService,
  countryId: undefined,
  version: '2026-09-14',
  effectiveFrom: '2026-09-14',
  language: 'cs',
  title: 'Obchodní podmínky',
  contentHtml: '<p>Úvod.</p>\n<h2>Přijetí podmínek</h2>\n<p>Text.</p>\n<h2>Objednávka &amp; platba</h2>\n<p>Ceny.</p>\n',
  contentHash: 'abc',
});

describe('LegalDocumentFacade', () => {
  let facade: LegalDocumentFacade;
  let store: MockStore;
  let getDocument: jest.Mock;
  let langChange$: Subject<{ lang: string }>;

  function build(): void {
    TestBed.resetTestingModule();
    TestBed.configureTestingModule({
      providers: [
        LegalDocumentFacade,
        provideMockStore({ selectors: [{ selector: selectMarketCountryId, value: 'cze-id' }] }),
        { provide: CustomerClient, useValue: { legalClient: { getDocument } } },
        { provide: TranslateService, useValue: { currentLang: 'cs', onLangChange: langChange$ } },
      ],
    });
    store = TestBed.inject(MockStore);
    facade = TestBed.inject(LegalDocumentFacade);
  }

  beforeEach(() => {
    langChange$ = new Subject();
    getDocument = jest.fn().mockReturnValue(of(TERMS));
    build();
  });

  afterEach(() => TestBed.resetTestingModule());

  describe('which text is read', () => {
    it('asks for the document of the given type in the chosen market and the current language', () => {
      facade.load(LegalDocumentType.PrivacyPolicy);

      expect(getDocument).toHaveBeenCalledWith(LegalDocumentType.PrivacyPolicy, 'cze-id', 'cs');
    });

    // The server resolves the default market when none is named (the no-market shape, ADR-0058 D3).
    it('sends no country when no market resolved', () => {
      store.overrideSelector(selectMarketCountryId, null);
      store.refreshState();

      facade.load(LegalDocumentType.TermsOfService);

      expect(getDocument).toHaveBeenCalledWith(LegalDocumentType.TermsOfService, undefined, 'cs');
    });

    it('re-reads the text when the customer switches market', () => {
      facade.load(LegalDocumentType.TermsOfService);
      store.overrideSelector(selectMarketCountryId, 'svk-id');
      store.refreshState();

      expect(getDocument).toHaveBeenCalledTimes(2);
      expect(getDocument).toHaveBeenLastCalledWith(LegalDocumentType.TermsOfService, 'svk-id', 'cs');
    });

    it('re-reads the text in the new language when the customer switches language', () => {
      facade.load(LegalDocumentType.TermsOfService);
      langChange$.next({ lang: 'en' });

      expect(getDocument).toHaveBeenLastCalledWith(LegalDocumentType.TermsOfService, 'cze-id', 'en');
      expect(facade.language()).toBe('en');
    });

    it('reads once per market and language, not once per emission', () => {
      facade.load(LegalDocumentType.TermsOfService);
      store.overrideSelector(selectMarketCountryId, 'cze-id');
      store.refreshState();
      langChange$.next({ lang: 'cs' });

      expect(getDocument).toHaveBeenCalledTimes(1);
    });
  });

  describe('the three states', () => {
    it('is loading until the server answers', () => {
      getDocument.mockReturnValue(new Subject());
      build();

      facade.load(LegalDocumentType.TermsOfService);

      expect(facade.loading()).toBe(true);
      expect(facade.hasError()).toBe(false);
      expect(facade.document()).toBeNull();
    });

    it('holds the served document once it arrives', () => {
      facade.load(LegalDocumentType.TermsOfService);

      expect(facade.loading()).toBe(false);
      expect(facade.hasError()).toBe(false);
      expect(facade.document()).toBe(TERMS);
    });

    it('reports the error state, with no document, when the read fails', () => {
      getDocument.mockReturnValue(throwError(() => new Error('offline')));
      build();

      facade.load(LegalDocumentType.TermsOfService);

      expect(facade.loading()).toBe(false);
      expect(facade.hasError()).toBe(true);
      expect(facade.document()).toBeNull();
    });

    it('clears a previous failure when a retry succeeds', () => {
      getDocument.mockReturnValueOnce(throwError(() => new Error('offline'))).mockReturnValue(of(TERMS));
      build();
      facade.load(LegalDocumentType.TermsOfService);
      expect(facade.hasError()).toBe(true);

      facade.retry();

      expect(getDocument).toHaveBeenCalledTimes(2);
      expect(facade.hasError()).toBe(false);
      expect(facade.document()).toBe(TERMS);
    });

    it('drops the stale document while a market switch is answered', () => {
      facade.load(LegalDocumentType.TermsOfService);
      getDocument.mockReturnValue(new Subject());

      store.overrideSelector(selectMarketCountryId, 'svk-id');
      store.refreshState();

      expect(facade.loading()).toBe(true);
      expect(facade.document()).toBeNull();
    });
  });

  // The contents rail lists the served text's own headings; the title-cased "&amp;" the renderer
  // escaped has to read as "&" on a button.
  it('lists the section headings of the served text', () => {
    facade.load(LegalDocumentType.TermsOfService);

    expect(facade.headings()).toEqual(['Přijetí podmínek', 'Objednávka & platba']);
  });

  it('lists no headings without a document', () => {
    getDocument.mockReturnValue(throwError(() => new Error('offline')));
    build();

    facade.load(LegalDocumentType.TermsOfService);

    expect(facade.headings()).toEqual([]);
  });
});
