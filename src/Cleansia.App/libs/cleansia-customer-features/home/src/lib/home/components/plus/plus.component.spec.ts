import { signal } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { CustomerAuthService, MembershipPlanFactsService } from '@cleansia/customer-services';
import { TranslateService } from '@ngx-translate/core';
import { PlusComponent } from './plus.component';
import { PlusFacade } from './plus.facade';

/**
 * Rendered without its template: the band's markup is covered by the home checkers, and what
 * is pinned here is the plan price label — which locale groups the digits. The market the plans
 * are read for and the trial offered are the facade's, and its spec pins them.
 */
describe('PlusComponent', () => {
  let component: PlusComponent;
  let load: jest.Mock;
  let translate: { currentLang: string };

  function build(currencyCode: string | null = 'CZK'): void {
    translate = { currentLang: 'en' };
    load = jest.fn();
    TestBed.configureTestingModule({
      providers: [
        PlusComponent,
        { provide: CustomerAuthService, useValue: { isLoggedIn: signal(false) } },
        {
          provide: MembershipPlanFactsService,
          useValue: { hasPlans: signal(false), currencyCode: signal(currencyCode) },
        },
        { provide: PlusFacade, useValue: { load } },
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

  it("labels the plan price with the plans' own currency, which is what a plan is priced in", () => {
    build('EUR');

    expect(component.formatPrice(199)).toContain('€');
  });

  it('loads the band through its facade when it starts', () => {
    build();

    component.ngOnInit();

    expect(load).toHaveBeenCalledTimes(1);
  });
});
