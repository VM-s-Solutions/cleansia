import { ComponentFixture, TestBed } from '@angular/core/testing';
import { selectMarketCurrencyCode, selectMarketNoShowCredit } from '@cleansia/customer-stores';
import { MockStore, provideMockStore } from '@ngrx/store/testing';
import { TranslateModule, TranslateService } from '@ngx-translate/core';
import { RulesComponent } from './rules.component';

describe('RulesComponent', () => {
  let fixture: ComponentFixture<RulesComponent>;
  let store: MockStore;
  let translate: TranslateService;

  const weCancelValue = (): string =>
    (fixture.nativeElement as HTMLElement)
      .querySelectorAll('.cl-rules__value')[2]
      ?.textContent?.trim() ?? '';

  async function render(
    lang: 'en' | 'cs',
    market: { noShowCredit: number | null; currencyCode: string | null },
  ): Promise<void> {
    TestBed.resetTestingModule();
    await TestBed.configureTestingModule({
      imports: [RulesComponent, TranslateModule.forRoot()],
      providers: [
        provideMockStore({
          selectors: [
            { selector: selectMarketNoShowCredit, value: market.noShowCredit },
            { selector: selectMarketCurrencyCode, value: market.currencyCode },
          ],
        }),
      ],
    }).compileComponents();
    store = TestBed.inject(MockStore);
    translate = TestBed.inject(TranslateService);
    translate.setTranslation('en', {
      pages: {
        home: {
          rules: {
            we_cancel_value: 'Everything back + {{amount}} credit',
            we_cancel_value_refund_only: 'Everything back',
          },
        },
      },
    });
    translate.setTranslation('cs', {
      pages: {
        home: {
          rules: {
            we_cancel_value: 'Vše zpět + kredit {{amount}}',
            we_cancel_value_refund_only: 'Vše zpět',
          },
        },
      },
    });
    translate.use(lang);
    fixture = TestBed.createComponent(RulesComponent);
    fixture.detectChanges();
  }

  // The figure is the market's `noShowCredit` (ADR-0060 D1), formatted on the client in the
  // market's currency; no locale file carries the digits.
  it('states the credit from the market, formatted in its currency for the reader\'s language', async () => {
    await render('cs', { noShowCredit: 250, currencyCode: 'CZK' });

    expect(weCancelValue()).toMatch(/Vše zpět \+ kredit 250\sKč/);
  });

  it('formats the same figure the way English groups it', async () => {
    await render('en', { noShowCredit: 250, currencyCode: 'CZK' });

    expect(weCancelValue()).toMatch(/Everything back \+ CZK\s?250 credit/);
  });

  // A market with no credit authored, and the no-market state: the refund promise alone, which
  // under-promises and is true everywhere. No amount, no empty placeholder.
  it('states the refund alone when the market has no credit', async () => {
    await render('en', { noShowCredit: null, currencyCode: 'EUR' });

    expect(weCancelValue()).toBe('Everything back');
  });

  it('states the refund alone when no market resolved', async () => {
    await render('en', { noShowCredit: null, currencyCode: null });

    expect(weCancelValue()).toBe('Everything back');
    expect(weCancelValue()).not.toContain('{{');
  });

  it('follows a market switch without a remount', async () => {
    await render('en', { noShowCredit: 250, currencyCode: 'CZK' });

    store.overrideSelector(selectMarketNoShowCredit, null);
    store.overrideSelector(selectMarketCurrencyCode, 'EUR');
    store.refreshState();
    fixture.detectChanges();

    expect(weCancelValue()).toBe('Everything back');
  });
});
