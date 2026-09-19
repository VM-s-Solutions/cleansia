import { TestBed } from '@angular/core/testing';
import { ServiceListItem } from '@cleansia/customer-services';
import {
  selectCustomerDefaultCurrencyCode,
  selectCustomerServices,
} from '@cleansia/customer-stores';
import { provideMockStore } from '@ngrx/store/testing';
import { TranslateService } from '@ngx-translate/core';
import { Subject } from 'rxjs';
import { ServicesComponent } from './services.component';

/**
 * Rendered without its template: the band's markup is covered by the home checkers, and what
 * is pinned here is the money label — which locale groups the digits and which code names the
 * currency.
 */
describe('ServicesComponent', () => {
  let component: ServicesComponent;
  let langChange: Subject<{ lang: string }>;

  function build(defaultCurrencyCode: string | null = 'CZK'): void {
    langChange = new Subject<{ lang: string }>();
    TestBed.configureTestingModule({
      providers: [
        ServicesComponent,
        provideMockStore({
          selectors: [
            { selector: selectCustomerServices, value: [] as ServiceListItem[] },
            { selector: selectCustomerDefaultCurrencyCode, value: defaultCurrencyCode },
          ],
        }),
        {
          provide: TranslateService,
          useValue: {
            currentLang: 'en',
            getDefaultLang: () => 'en',
            onLangChange: langChange,
          },
        },
      ],
    });
    component = TestBed.inject(ServicesComponent);
  }

  afterEach(() => TestBed.resetTestingModule());

  // The locale was a `'cs-CZ'` literal, so an English reader got Czech digit grouping and symbol
  // placement on the home page while every order screen formatted per language.
  it("groups and places the symbol the way the reader's language does", () => {
    build('CZK');

    expect(component.formatPrice(1200)).toMatch(/CZK\s?1,200/);
  });

  it('follows a language change', () => {
    build('CZK');

    langChange.next({ lang: 'cs' });

    expect(component.formatPrice(1200)).toMatch(/1\s200\sKč/);
  });

  // Every catalogue item says which currency it is priced in; the platform default labels only
  // the fallback figures, which have no item behind them.
  it("labels a price with the item's own code, and a bare figure with the default", () => {
    build('CZK');

    expect(component.formatPrice(40, 'EUR')).toContain('€');
    expect(component.formatPrice(40, 'EUR')).not.toContain('CZK');
    expect(component.formatPrice(890)).toContain('CZK');
  });

  it('prints a bare number until any currency is known', () => {
    build(null);

    expect(component.formatPrice(890)).toBe('890');
  });
});
