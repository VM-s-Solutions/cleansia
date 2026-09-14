import { Type } from '@angular/core';
import { ComponentFixture, TestBed } from '@angular/core/testing';
import { CustomerClient, LegalDocumentDto, LegalDocumentType } from '@cleansia/customer-services';
import { selectMarketCountryId } from '@cleansia/customer-stores';
import { provideMockStore } from '@ngrx/store/testing';
import { TranslateModule, TranslateService } from '@ngx-translate/core';
import { Observable, Subject, of, throwError } from 'rxjs';
import { PrivacyComponent } from '../privacy/privacy.component';
import { TermsComponent } from '../terms/terms.component';
import { LegalDocumentComponent } from './legal-document.component';

const SERVED = LegalDocumentDto.fromJS({
  type: LegalDocumentType.TermsOfService,
  countryId: 'cze-id',
  version: '2026-09-14',
  effectiveFrom: '2026-09-14',
  language: 'en',
  title: 'Served Terms',
  contentHtml:
    '<p>Welcome.</p>\n<h2>Acceptance of Terms</h2>\n<p>A.</p>\n<h2>Ordering &amp; Payment</h2>\n<p>B.</p>\n',
  contentHash: 'abc',
});

const DICTIONARY = {
  legal: {
    eyebrow: 'Legal information',
    contents: 'Contents',
    ask_lead: 'Something unclear? Write to',
    ask_tail: '— we answer like people.',
    version: 'Version {{version}}',
    effective_from: 'Effective from {{date}}',
    load_error: "We couldn't load this document.",
  },
  global: { actions: { retry: 'Retry' } },
  terms_page: { title: 'Terms of Service' },
  privacy_page: { title: 'Privacy Policy' },
};

describe('LegalDocumentComponent', () => {
  let fixture: ComponentFixture<unknown>;
  let getDocument: jest.Mock;

  const host = (): HTMLElement => fixture.nativeElement as HTMLElement;
  const text = (selector: string): string | undefined =>
    host().querySelector(selector)?.textContent?.trim();

  async function render(
    answer: Observable<LegalDocumentDto>,
    component: Type<unknown> = LegalDocumentComponent,
  ): Promise<void> {
    getDocument = jest.fn().mockReturnValue(answer);
    await TestBed.configureTestingModule({
      imports: [component, TranslateModule.forRoot()],
      providers: [
        provideMockStore({ selectors: [{ selector: selectMarketCountryId, value: 'cze-id' }] }),
        { provide: CustomerClient, useValue: { legalClient: { getDocument } } },
      ],
    }).compileComponents();
    const translate = TestBed.inject(TranslateService);
    translate.setTranslation('en', DICTIONARY);
    translate.use('en');

    fixture = TestBed.createComponent(component);
    if (component === LegalDocumentComponent) {
      fixture.componentRef.setInput('type', LegalDocumentType.TermsOfService);
      fixture.componentRef.setInput('titleKey', 'terms_page.title');
    }
    fixture.detectChanges();
  }

  afterEach(() => TestBed.resetTestingModule());

  describe('the served document', () => {
    beforeEach(() => render(of(SERVED)));

    it('claims the one h1 for the served title', () => {
      const headings = host().querySelectorAll('h1');

      expect(headings.length).toBe(1);
      expect(headings[0].textContent?.trim()).toBe('Served Terms');
    });

    it('states the effective date, localized', () => {
      expect(text('.cl-lgl__updated')).toBe('Effective from September 14, 2026');
    });

    // The version is what a consent row stamps, so it has to be readable verbatim on the page.
    it('states the version string the server stamps on a consent', () => {
      expect(text('.cl-lgl__version')).toBe('Version 2026-09-14');
    });

    it('renders the server-rendered HTML as the content', () => {
      const content = host().querySelector('.cl-lgl__content');
      const sections = Array.from(content?.querySelectorAll('h2') ?? []).map((h) => h.textContent);

      expect(sections).toEqual(['Acceptance of Terms', 'Ordering & Payment']);
      expect(content?.querySelector('p')?.textContent).toBe('Welcome.');
    });

    it('lists the served sections, numbered, on the contents rail', () => {
      const items = Array.from(host().querySelectorAll('.cl-lgl__toc-item')).map((b) =>
        b.textContent?.replace(/\s+/g, ' ').trim(),
      );

      expect(items).toEqual(['1. Acceptance of Terms', '2. Ordering & Payment']);
    });

    it('shows neither the skeleton nor the error', () => {
      expect(host().querySelector('p-skeleton')).toBeNull();
      expect(host().querySelector('.cl-lgl__error')).toBeNull();
    });
  });

  describe('while the server has not answered', () => {
    beforeEach(() => render(new Subject<LegalDocumentDto>()));

    it('shows the skeleton and no content', () => {
      expect(host().querySelector('p-skeleton')).not.toBeNull();
      expect(host().querySelector('.cl-lgl__content')).toBeNull();
      expect(host().querySelector('.cl-lgl__error')).toBeNull();
    });

    it('names the page from its own key until the served title arrives', () => {
      expect(text('h1')).toBe('Terms of Service');
      expect(host().querySelector('.cl-lgl__version')).toBeNull();
    });
  });

  describe('when the read fails', () => {
    beforeEach(() => render(throwError(() => new Error('offline'))));

    it('shows the error and no content', () => {
      expect(text('.cl-lgl__error')).toContain("We couldn't load this document.");
      expect(host().querySelector('.cl-lgl__content')).toBeNull();
      expect(host().querySelector('p-skeleton')).toBeNull();
    });

    it('reads the document again when the retry button is pressed', () => {
      getDocument.mockReturnValue(of(SERVED));

      host().querySelector<HTMLButtonElement>('.cl-lgl__error button')?.click();
      fixture.detectChanges();

      expect(getDocument).toHaveBeenCalledTimes(2);
      expect(host().querySelector('.cl-lgl__error')).toBeNull();
      expect(text('.cl-lgl__version')).toBe('Version 2026-09-14');
    });
  });

  // The two pages are this component with a document type each; a swapped type would show the
  // privacy text under the terms title.
  const PAGES: { page: string; component: Type<unknown>; type: LegalDocumentType; title: string }[] = [
    { page: 'terms', component: TermsComponent, type: LegalDocumentType.TermsOfService, title: 'Terms of Service' },
    { page: 'privacy', component: PrivacyComponent, type: LegalDocumentType.PrivacyPolicy, title: 'Privacy Policy' },
  ];
  describe.each(PAGES)('the $page page', ({ component, type, title }) => {
    it('asks for its own document type in the chosen market and names itself meanwhile', async () => {
      await render(new Subject<LegalDocumentDto>(), component);

      expect(getDocument).toHaveBeenCalledWith(type, 'cze-id', 'en');
      expect(text('h1')).toBe(title);
    });
  });
});
