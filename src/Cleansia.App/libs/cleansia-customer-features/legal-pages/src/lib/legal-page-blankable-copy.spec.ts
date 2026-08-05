import { Type } from '@angular/core';
import { ComponentFixture, TestBed } from '@angular/core/testing';
import { TranslateModule, TranslateService } from '@ngx-translate/core';
import { PrivacyComponent } from './privacy/privacy.component';
import { TermsComponent } from './terms/terms.component';

interface LegalPage {
  readonly name: string;
  readonly component: Type<{ sections: number[] }>;
  readonly namespace: string;
}

const PAGES: LegalPage[] = [
  { name: 'privacy', component: PrivacyComponent, namespace: 'privacy_page' },
  { name: 'terms', component: TermsComponent, namespace: 'terms_page' },
];

const SECTION_COUNT = 6;

function sectionCopy(): Record<string, string> {
  const copy: Record<string, string> = {};
  for (let i = 1; i <= SECTION_COUNT; i++) {
    copy[`section${i}_title`] = `Section ${i} title`;
    copy[`section${i}_text`] = `Section ${i} text`;
  }
  return copy;
}

describe.each(PAGES)('$name legal page', ({ component, namespace }) => {
  let fixture: ComponentFixture<{ sections: number[] }>;
  let translate: TranslateService;

  function render(overrides: Record<string, string>): void {
    translate.setTranslation(
      'en',
      {
        [namespace]: {
          title: 'Legal title',
          intro: 'Legal intro',
          last_updated: 'Last updated on {{date}}',
          review_notice: '',
          last_updated_date: '',
          ...sectionCopy(),
          ...overrides,
        },
      },
      true
    );
    fixture.detectChanges();
  }

  const host = (): HTMLElement => fixture.nativeElement as HTMLElement;
  const notice = (): HTMLElement | null => host().querySelector('.legal-page__notice-text');
  const meta = (): HTMLElement | null => host().querySelector('.legal-page__meta');

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [component, TranslateModule.forRoot()],
    }).compileComponents();

    translate = TestBed.inject(TranslateService);
    translate.use('en');
    fixture = TestBed.createComponent(component);
  });

  it('hides the review notice and the last-updated line while the owner leaves them blank', () => {
    render({});

    expect(notice()).toBeNull();
    expect(meta()).toBeNull();
  });

  it('shows the review notice the moment the owner fills the key in', () => {
    render({ review_notice: 'This text is under legal review.' });

    expect(notice()?.textContent?.trim()).toBe('This text is under legal review.');
  });

  it('interpolates the owner-supplied date into the last-updated line', () => {
    render({ last_updated_date: '2026-08-05' });

    expect(meta()?.textContent?.trim()).toBe('Last updated on 2026-08-05');
  });

  it('renders one block per section, keyed by index', () => {
    render({});

    const titles = Array.from(
      host().querySelectorAll<HTMLElement>('.legal-page__section-title')
    ).map((el) => el.textContent?.trim());

    expect(titles).toEqual([
      'Section 1 title',
      'Section 2 title',
      'Section 3 title',
      'Section 4 title',
      'Section 5 title',
      'Section 6 title',
    ]);
  });

  it('claims the page h1 for the page title', () => {
    render({});

    const headings = host().querySelectorAll('h1');
    expect(headings.length).toBe(1);
    expect(headings[0].textContent?.trim()).toBe('Legal title');
  });
});
