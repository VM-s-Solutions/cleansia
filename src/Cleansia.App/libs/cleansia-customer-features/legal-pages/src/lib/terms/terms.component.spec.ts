import { ComponentFixture, TestBed } from '@angular/core/testing';
import { selectMarketCurrencyCode } from '@cleansia/customer-stores';
import { MockStore, provideMockStore } from '@ngrx/store/testing';
import { TranslateModule, TranslateService } from '@ngx-translate/core';
import { TermsComponent } from './terms.component';

describe('TermsComponent — the currency in section 3', () => {
  let fixture: ComponentFixture<TermsComponent>;
  let store: MockStore;

  const sectionText = (index: number): string =>
    (fixture.nativeElement as HTMLElement)
      .querySelectorAll('.cl-lgl__section-text')[index - 1]
      ?.textContent?.trim() ?? '';

  async function render(currencyCode: string | null): Promise<void> {
    TestBed.resetTestingModule();
    await TestBed.configureTestingModule({
      imports: [TermsComponent, TranslateModule.forRoot()],
      providers: [
        provideMockStore({
          selectors: [{ selector: selectMarketCurrencyCode, value: currencyCode }],
        }),
      ],
    }).compileComponents();
    store = TestBed.inject(MockStore);
    const translate = TestBed.inject(TranslateService);
    translate.setTranslation('en', {
      terms_page: {
        title: 'Terms',
        intro: 'Intro',
        review_notice: '',
        last_updated: '{{date}}',
        last_updated_date: '',
        section1_title: 'One',
        section1_text: 'First',
        section3_title: 'Ordering',
        section3_text: 'Prices are displayed in {{currency}} and are final.',
        section3_text_no_market: 'Prices are shown in the currency of the country you are booking in.',
      },
    });
    translate.use('en');
    fixture = TestBed.createComponent(TermsComponent);
    fixture.detectChanges();
  }

  // The unit is the market's code (ADR-0060 D3), never written into a locale file.
  it('names the market currency', async () => {
    await render('EUR');

    expect(sectionText(3)).toBe('Prices are displayed in EUR and are final.');
  });

  // The no-market state renders the market-agnostic sentence, not an empty placeholder in a
  // legal paragraph.
  it('states the market-agnostic sentence when no market resolved', async () => {
    await render(null);

    expect(sectionText(3)).toBe('Prices are shown in the currency of the country you are booking in.');
    expect(sectionText(3)).not.toContain('{{');
  });

  it('leaves the other sections alone', async () => {
    await render('EUR');

    expect(sectionText(1)).toBe('First');
  });

  it('follows a market switch', async () => {
    await render('EUR');

    store.overrideSelector(selectMarketCurrencyCode, 'CZK');
    store.refreshState();
    fixture.detectChanges();

    expect(sectionText(3)).toBe('Prices are displayed in CZK and are final.');
  });
});
