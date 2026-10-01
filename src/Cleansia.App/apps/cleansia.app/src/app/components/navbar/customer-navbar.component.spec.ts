import { Component, input, output, signal } from '@angular/core';
import { ComponentFixture, TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { CleansiaBrandNameComponent } from '@cleansia/components/cleansia-brand-name';
import { CleansiaLanguageSwitcherComponent } from '@cleansia/components/cleansia-language-switcher';
import { CleansiaMarketSwitcherComponent } from '@cleansia/components/cleansia-market-switcher';
import { CustomerAuthService, MarketListItem } from '@cleansia/customer-services';
import {
  selectCustomerCurrentUser,
  selectHasMarketChoice,
  selectMarket,
  selectMarkets,
} from '@cleansia/customer-stores';
import { DialogService, ThemeService } from '@cleansia/services';
import { MockStore, provideMockStore } from '@ngrx/store/testing';
import { TranslateModule } from '@ngx-translate/core';
import { of } from 'rxjs';
import { CleansiaCustomerNavbarComponent } from './customer-navbar.component';

@Component({ selector: 'cleansia-brand-name', template: '' })
class BrandNameStubComponent {
  readonly defaultRoute = input('');
}

@Component({ selector: 'cleansia-language-switcher', template: '' })
class LanguageSwitcherStubComponent {
  readonly variant = input('');
}

@Component({ selector: 'cleansia-market-switcher', template: '' })
class MarketSwitcherStubComponent {
  readonly variant = input('');
  readonly markets = input<readonly unknown[]>([]);
  readonly selected = input<string | null>(null);
  readonly marketChange = output<string>();
}

function market(isoCode: string): MarketListItem {
  return MarketListItem.fromJS({ countryId: `${isoCode}-id`, isoCode, currencyCode: 'CZK' });
}

/**
 * Below 1360px the sheet is the only navigation (jsdom's window is 1024 wide). It is a disclosure
 * the burger owns: its state is announced on the burger's focusable inner button, the page is
 * locked under it, and every way of closing it hands focus back to the burger.
 */
describe('CleansiaCustomerNavbarComponent (mobile sheet)', () => {
  let fixture: ComponentFixture<CleansiaCustomerNavbarComponent>;
  const isLoggedIn = signal(true);

  beforeEach(async () => {
    isLoggedIn.set(true);
    await TestBed.configureTestingModule({
      imports: [CleansiaCustomerNavbarComponent, TranslateModule.forRoot()],
      providers: [
        provideRouter([]),
        provideMockStore({
          selectors: [
            { selector: selectCustomerCurrentUser, value: undefined },
            { selector: selectMarkets, value: [] },
            { selector: selectHasMarketChoice, value: false },
            { selector: selectMarket, value: null },
          ],
        }),
        { provide: CustomerAuthService, useValue: { isLoggedIn, logout: () => of(undefined) } },
        { provide: DialogService, useValue: { confirmTranslated: () => of(false) } },
        { provide: ThemeService, useValue: { currentTheme: signal('light'), toggleTheme: jest.fn() } },
      ],
    })
      .overrideComponent(CleansiaCustomerNavbarComponent, {
        remove: {
          imports: [
            CleansiaBrandNameComponent,
            CleansiaLanguageSwitcherComponent,
            CleansiaMarketSwitcherComponent,
          ],
        },
        add: {
          imports: [BrandNameStubComponent, LanguageSwitcherStubComponent, MarketSwitcherStubComponent],
        },
      })
      .compileComponents();

    fixture = TestBed.createComponent(CleansiaCustomerNavbarComponent);
    // Attached to the app's ticks, as the real bar is: the focus moves run after a render.
    fixture.autoDetectChanges();
  });

  afterEach(() => {
    document.documentElement.classList.remove('cl-menu-open');
    document.body.classList.remove('cl-menu-open');
  });

  function burger(): HTMLButtonElement {
    return fixture.nativeElement.querySelector('.customer-navbar__hamburger button');
  }

  function sheet(): HTMLElement | null {
    return fixture.nativeElement.querySelector('#cl-mobile-nav');
  }

  function open(): void {
    burger().click();
    fixture.detectChanges();
  }

  function sheetLinks(): HTMLAnchorElement[] {
    return Array.from(sheet()?.querySelectorAll('a') ?? []);
  }

  function linkTo(href: string): HTMLAnchorElement | undefined {
    return sheetLinks().find((link) => link.getAttribute('href') === href);
  }

  function useMarkets(markets: MarketListItem[]): void {
    const store = TestBed.inject(MockStore);
    store.overrideSelector(selectMarkets, markets);
    store.overrideSelector(selectHasMarketChoice, selectHasMarketChoice.projector(markets));
    store.refreshState();
    fixture.detectChanges();
  }

  function expectClosed(): void {
    expect(sheet()).toBeNull();
    expect(burger().getAttribute('aria-expanded')).toBe('false');
    expect(document.documentElement.classList.contains('cl-menu-open')).toBe(false);
    expect(document.body.classList.contains('cl-menu-open')).toBe(false);
    expect(document.activeElement).toBe(burger());
  }

  it('opens inside the bar, says so on the inner button, locks the page and focuses its first link', () => {
    expect(burger().getAttribute('aria-expanded')).toBe('false');
    expect(burger().getAttribute('aria-controls')).toBe('cl-mobile-nav');

    open();

    expect(burger().getAttribute('aria-expanded')).toBe('true');
    expect(sheet()?.closest('.customer-navbar')).toBe(fixture.nativeElement.querySelector('.customer-navbar'));
    expect(document.documentElement.classList.contains('cl-menu-open')).toBe(true);
    expect(document.body.classList.contains('cl-menu-open')).toBe(true);
    expect(document.activeElement).toBe(sheet()?.querySelector('a'));
  });

  it('closes on Escape and returns focus to the burger', () => {
    open();

    document.dispatchEvent(new KeyboardEvent('keydown', { key: 'Escape' }));
    fixture.detectChanges();

    expectClosed();
  });

  it('closes on a backdrop click and returns focus to the burger', () => {
    open();

    fixture.nativeElement.querySelector('.customer-navbar__backdrop').click();
    fixture.detectChanges();

    expectClosed();
  });

  it('closes on the burger and keeps focus there', () => {
    open();

    burger().click();
    fixture.detectChanges();

    expectClosed();
  });

  it('stays open when the page scrolls under it', () => {
    open();
    Object.defineProperty(window, 'scrollY', { value: 500, configurable: true });

    try {
      fixture.componentInstance.onScroll();
      fixture.detectChanges();
    } finally {
      Object.defineProperty(window, 'scrollY', { value: 0, configurable: true });
    }

    expect(sheet()).not.toBeNull();
  });

  it('names the burger, the sheet and the account menu through translation keys', () => {
    expect(burger().getAttribute('aria-label')).toBe('nav.open_menu');
    expect(
      fixture.nativeElement.querySelector('.customer-navbar__menu-anchor button').getAttribute('aria-label'),
    ).toBe('nav.user_menu');

    open();

    expect(burger().getAttribute('aria-label')).toBe('nav.close_menu');
    expect(sheet()?.getAttribute('aria-label')).toBe('nav.section_menu');
  });

  it('signed in, opens on the order button and lists Cleansia Plus once', () => {
    open();

    expect(sheetLinks()[0].getAttribute('href')).toBe('/order');
    const plus = sheetLinks().filter((link) => ['/plus', '/membership'].includes(link.getAttribute('href') ?? ''));
    expect(plus.map((link) => link.getAttribute('href'))).toEqual(['/membership']);
    expect(linkTo('/profile')).toBeDefined();
  });

  it('signed out, offers sign-in and registration side by side and no account group', () => {
    isLoggedIn.set(false);
    fixture.detectChanges();
    open();

    const login = linkTo('/login');
    const register = linkTo('/register');
    expect(login?.textContent?.trim()).toBe('nav.login_short');
    expect(register?.textContent?.trim()).toBe('nav.register');
    expect(login?.parentElement).toBe(register?.parentElement);
    expect(login?.parentElement?.classList).toContain('customer-navbar__group--auth');

    for (const href of ['/rewards', '/membership/recurring', '/disputes', '/profile']) {
      expect(linkTo(href)).toBeUndefined();
    }
    expect(sheet()?.textContent).not.toContain('nav.section_account');
    expect(sheet()?.textContent).not.toContain('nav.logout');
  });

  it('shows the market pill beside the language pill only with two or more markets', () => {
    useMarkets([market('CZE')]);
    open();
    const prefs = (): HTMLElement | null | undefined => sheet()?.querySelector('.customer-navbar__prefs');

    expect(prefs()?.querySelector('cleansia-language-switcher')).toBeTruthy();
    expect(prefs()?.querySelector('cleansia-market-switcher')).toBeNull();

    useMarkets([market('CZE'), market('SVK')]);

    expect(prefs()?.querySelector('cleansia-market-switcher')).toBeTruthy();
  });
});
