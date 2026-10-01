import { PLATFORM_ID, Provider, Type } from '@angular/core';
import { ComponentFixture, TestBed } from '@angular/core/testing';
import { CustomerClient, LegalDocumentDto, LegalDocumentType } from '@cleansia/customer-services';
import { selectMarketCountryId } from '@cleansia/customer-stores';
import { provideMockStore } from '@ngrx/store/testing';
import { TranslateModule, TranslateService } from '@ngx-translate/core';
import { Observable, Subject, of, throwError } from 'rxjs';
import { PrivacyComponent } from '../privacy/privacy.component';
import { TermsComponent } from '../terms/terms.component';
import { ComplaintsComponent } from '../complaints/complaints.component';
import { LegalDocumentComponent } from './legal-document.component';

const SERVED = LegalDocumentDto.fromJS({
  type: LegalDocumentType.TermsOfService,
  countryId: 'cze-id',
  version: '2026-09-14',
  effectiveFrom: '2026-09-14',
  language: 'en',
  title: 'Served Terms',
  contentHtml:
    '<p>Welcome.</p>\n<h2>1. Acceptance of Terms</h2>\n<p>A.</p>\n<h2>2. Ordering &amp; Payment</h2>\n<p>B.</p>\n',
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
  complaints_page: { title: 'Complaints Procedure' },
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
    providers: Provider[] = [],
  ): Promise<void> {
    getDocument = jest.fn().mockReturnValue(answer);
    await TestBed.configureTestingModule({
      imports: [component, TranslateModule.forRoot()],
      providers: [
        provideMockStore({ selectors: [{ selector: selectMarketCountryId, value: 'cze-id' }] }),
        { provide: CustomerClient, useValue: { legalClient: { getDocument } } },
        ...providers,
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

  // The rail follows the reader with scroll APIs jsdom implements none of.
  beforeAll(() => {
    if (!window.matchMedia) {
      window.matchMedia = (media: string) => ({ matches: false, media }) as MediaQueryList;
    }
    if (!Element.prototype.scrollIntoView) {
      Element.prototype.scrollIntoView = () => undefined;
    }
    if (!Element.prototype.scrollTo) {
      Element.prototype.scrollTo = () => undefined;
    }
  });

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

      expect(sections).toEqual(['1 Acceptance of Terms', '2 Ordering & Payment']);
      expect(content?.querySelector('p')?.textContent).toBe('Welcome.');
    });

    it("sets each section heading's own number apart, past the sanitizer", () => {
      const numbers = Array.from(host().querySelectorAll('.cl-lgl__content h2 .cl-lgl__num')).map(
        (n) => n.textContent,
      );

      expect(numbers).toEqual(['1', '2']);
    });

    it("lists the served sections on the contents rail under the document's own numbers, each once", () => {
      const items = Array.from(host().querySelectorAll('.cl-lgl__toc-item')).map((b) => ({
        num: b.querySelector('.cl-lgl__toc-num')?.textContent?.trim(),
        title: b.lastElementChild?.textContent?.trim(),
        numbers: b.textContent?.match(/\d+/g),
      }));

      expect(items).toEqual([
        { num: '1', title: 'Acceptance of Terms', numbers: ['1'] },
        { num: '2', title: 'Ordering & Payment', numbers: ['2'] },
      ]);
    });

    it('shows neither the skeleton nor the error', () => {
      expect(host().querySelector('p-skeleton')).toBeNull();
      expect(host().querySelector('.cl-lgl__error')).toBeNull();
    });
  });

  it('numbers the sections of an unnumbered document by position', async () => {
    await render(of(LegalDocumentDto.fromJS({ ...SERVED.toJSON(), contentHtml: '<h2>Scope</h2><h2>Contact</h2>' })));

    const numbers = Array.from(host().querySelectorAll('.cl-lgl__toc-num')).map((n) => n.textContent?.trim());

    expect(numbers).toEqual(['1', '2']);
  });

  describe('the rail following the reader', () => {
    const component = (): LegalDocumentComponent => fixture.componentInstance as LegalDocumentComponent;
    const headings = (): HTMLElement[] =>
      Array.from(host().querySelectorAll<HTMLElement>('.cl-lgl__content h2'));
    const scrollPage = (): void => {
      window.dispatchEvent(new Event('scroll'));
      jest.advanceTimersByTime(16);
    };

    beforeEach(async () => {
      await render(of(SERVED));
      jest.useFakeTimers();
    });

    afterEach(() => {
      jest.useRealTimers();
      jest.restoreAllMocks();
      Reflect.deleteProperty(document.documentElement, 'scrollHeight');
    });

    // jsdom has no layout, so the page always reads as scrolled to the bottom and the sync lights
    // the last section: that is the sync a jump must hold off.
    it('lights the clicked entry at once and holds it until the page scroll ends', () => {
      component().jumpTo(0);
      expect(component().activeSection()).toBe(0);

      scrollPage();
      expect(component().activeSection()).toBe(0);

      window.dispatchEvent(new Event('scrollend'));
      scrollPage();
      expect(component().activeSection()).toBe(1);
    });

    it('lets go of the clicked entry once the page has been still for 700ms, where scrollend never comes', () => {
      component().jumpTo(0);
      jest.advanceTimersByTime(600);
      scrollPage();
      jest.advanceTimersByTime(150);
      scrollPage();
      expect(component().activeSection()).toBe(0);

      jest.advanceTimersByTime(700);
      scrollPage();
      expect(component().activeSection()).toBe(1);
    });

    it.each([
      { offset: '96px', lit: 1 },
      { offset: '0px', lit: 0 },
    ])('counts a heading as read once it crosses $offset of navbar plus 24px', ({ offset, lit }) => {
      Object.defineProperty(document.documentElement, 'scrollHeight', { configurable: true, value: 10_000 });
      jest.spyOn(headings()[0], 'getBoundingClientRect').mockReturnValue({ top: -400 } as DOMRect);
      jest.spyOn(headings()[1], 'getBoundingClientRect').mockReturnValue({ top: 100 } as DOMRect);
      jest.spyOn(window, 'getComputedStyle').mockReturnValue({
        getPropertyValue: (name: string) => (name === '--cl-nav-offset' ? offset : ''),
      } as CSSStyleDeclaration);
      component().activeSection.set(0);

      scrollPage();

      expect(component().activeSection()).toBe(lit);
    });

    it('scrolls the list, not the card, to centre the lit entry', () => {
      const scrollTo = jest.spyOn(Element.prototype, 'scrollTo');
      const list = host().querySelector<HTMLElement>('.cl-lgl__toc-list');
      const first = host().querySelector<HTMLElement>('.cl-lgl__toc-item');
      if (!list || !first) throw new Error('the rail did not render');
      Object.defineProperty(list, 'clientHeight', { configurable: true, value: 400 });
      Object.defineProperty(first, 'offsetTop', { configurable: true, value: 300 });
      Object.defineProperty(first, 'offsetHeight', { configurable: true, value: 40 });

      component().activeSection.set(0);
      fixture.detectChanges();

      expect(scrollTo).toHaveBeenCalledTimes(1);
      expect(scrollTo.mock.contexts[0]).toBe(list);
      expect(scrollTo).toHaveBeenCalledWith({ top: 120, behavior: 'smooth' });
    });
  });

  it('leaves the rail list alone when rendered on the server', async () => {
    const scrollTo = jest.spyOn(Element.prototype, 'scrollTo');

    await render(of(SERVED), LegalDocumentComponent, [{ provide: PLATFORM_ID, useValue: 'server' }]);
    (fixture.componentInstance as LegalDocumentComponent).activeSection.set(1);
    fixture.detectChanges();

    expect(host().querySelector('.cl-lgl__toc-list')).not.toBeNull();
    expect(scrollTo).not.toHaveBeenCalled();
    scrollTo.mockRestore();
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

  // The three pages are this component with a document type each; a swapped type would show the
  // privacy text under the terms title. A question about personal data goes to the privacy address.
  const PAGES: {
    page: string;
    component: Type<unknown>;
    type: LegalDocumentType;
    title: string;
    contact: string;
  }[] = [
    {
      page: 'terms',
      component: TermsComponent,
      type: LegalDocumentType.TermsOfService,
      title: 'Terms of Service',
      contact: 'info@cleansia.cz',
    },
    {
      page: 'privacy',
      component: PrivacyComponent,
      type: LegalDocumentType.PrivacyPolicy,
      title: 'Privacy Policy',
      contact: 'privacy@cleansia.cz',
    },
    {
      page: 'complaints',
      component: ComplaintsComponent,
      type: LegalDocumentType.ComplaintsProcedure,
      title: 'Complaints Procedure',
      contact: 'info@cleansia.cz',
    },
  ];
  describe.each(PAGES)('the $page page', ({ component, type, title, contact }) => {
    it('asks for its own document type in the chosen market and names itself meanwhile', async () => {
      await render(new Subject<LegalDocumentDto>(), component);

      expect(getDocument).toHaveBeenCalledWith(type, 'cze-id', 'en');
      expect(text('h1')).toBe(title);
    });

    it(`points a question at ${contact}`, async () => {
      await render(of(SERVED), component);

      const link = host().querySelector<HTMLAnchorElement>('.cl-lgl__ask a');
      expect(link?.getAttribute('href')).toBe(`mailto:${contact}`);
      expect(link?.textContent?.trim()).toBe(contact);
    });
  });
});
