import { signal } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { CustomerAuthService, MembershipPlanFactsService } from '@cleansia/customer-services';
import { selectCustomerDefaultCurrencyCode } from '@cleansia/customer-stores';
import { provideMockStore } from '@ngrx/store/testing';
import { TranslateService } from '@ngx-translate/core';
import { PlusComponent } from './plus.component';

/**
 * Rendered without its template: the band's markup is covered by the home checkers, and what
 * is pinned here is the plan price label — which locale groups the digits.
 */
describe('PlusComponent', () => {
  let component: PlusComponent;
  let translate: { currentLang: string };

  function build(defaultCurrencyCode: string | null = 'CZK'): void {
    translate = { currentLang: 'en' };
    TestBed.configureTestingModule({
      providers: [
        PlusComponent,
        provideMockStore({
          selectors: [{ selector: selectCustomerDefaultCurrencyCode, value: defaultCurrencyCode }],
        }),
        { provide: CustomerAuthService, useValue: { isLoggedIn: signal(false) } },
        {
          provide: MembershipPlanFactsService,
          useValue: { load: jest.fn(), hasPlans: signal(false) },
        },
        { provide: TranslateService, useValue: translate },
      ],
    });
    component = TestBed.inject(PlusComponent);
  }

  afterEach(() => TestBed.resetTestingModule());

  // The locale was a `'cs-CZ'` literal, so an English reader got Czech digit grouping and symbol
  // placement on the Plus band while every order screen formatted per language.
  it("groups and places the symbol the way the reader's language does", () => {
    build('CZK');

    translate.currentLang = 'en';
    expect(component.formatPrice(1200)).toMatch(/CZK\s?1,200/);

    translate.currentLang = 'cs';
    expect(component.formatPrice(1200)).toMatch(/1\s200\sKč/);
  });

  it('labels the plan price with the platform default, which is what a plan is priced in', () => {
    build('EUR');

    expect(component.formatPrice(199)).toContain('€');
  });
});
