import { ChangeDetectionStrategy, Component, computed, inject } from '@angular/core';
import { toSignal } from '@angular/core/rxjs-interop';
import { RouterModule } from '@angular/router';
import { CustomerAuthService } from '@cleansia/customer-services';
import { chooseMarket, selectMarket, selectMarkets } from '@cleansia/customer-stores';
import { ThemeService } from '@cleansia/services';
import { Store } from '@ngrx/store';
import { TranslatePipe } from '@ngx-translate/core';
// Entry-point imports — see the note in app.ts. → T-0682
import { CleansiaLanguageSwitcherComponent } from '@cleansia/components/cleansia-language-switcher';
import { CleansiaMarketSwitcherComponent } from '@cleansia/components/cleansia-market-switcher';
import { CookieConsentService } from '@cleansia/components/cleansia-cookie-consent';

@Component({
  selector: 'cleansia-customer-footer',
  templateUrl: './customer-footer.component.html',
  standalone: true,
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [
    RouterModule,
    TranslatePipe,
    CleansiaLanguageSwitcherComponent,
    CleansiaMarketSwitcherComponent,
  ],
})
export class CleansiaCustomerFooterComponent {
  private readonly authService = inject(CustomerAuthService);
  private readonly themeService = inject(ThemeService);
  private readonly cookieConsent = inject(CookieConsentService);
  private readonly store = inject(Store);

  // The same market control the header carries, so the two cannot disagree.
  readonly markets = toSignal(this.store.select(selectMarkets), { initialValue: [] });
  private readonly market = toSignal(this.store.select(selectMarket), { initialValue: null });
  readonly selectedMarketCode = computed(() => this.market()?.isoCode ?? null);

  onMarketChange(isoCode: string): void {
    this.store.dispatch(chooseMarket({ isoCode }));
  }

  // Same source of truth as the header control, so the two never disagree.
  readonly isDarkMode = computed(() => this.themeService.currentTheme() === 'dark');

  currentYear = new Date().getFullYear();

  /**
   * The service column, as the artboard lists it. Every row points at the
   * catalogue: these are service names, and the catalogue is where they live.
   */
  readonly serviceLinks = [
    'pages.home.footer.svc_home',
    'pages.home.footer.svc_deep',
    'pages.home.footer.svc_carpet',
    'pages.home.footer.svc_upholstery',
    'pages.home.footer.svc_bathroom',
    'pages.home.footer.svc_windows',
    'pages.home.footer.svc_renovation',
  ];

  // Hide the guest lookup link for logged-in users — they have /orders.
  // Reactive on the service signal so the footer updates immediately on
  // sign-in/sign-out without remount.
  readonly isAnonymous = computed(() => !this.authService.isLoggedIn());

  /**
   * The footer renders on every page, so this link reached every anonymous
   * visitor — and pointed at `/membership`, which is behind `customerAuthGuard`
   * and bounced them to a login form. It opens the public Plus page for them
   * and the management screen for a member, matching the nav bar's Plus link.
   */
  readonly plusLink = computed(() => (this.isAnonymous() ? '/plus' : '/membership'));

  /** Reopens the cookie banner on its settings panel. */
  openCookieSettings(): void {
    this.cookieConsent.openSettings();
  }

  toggleTheme(): void {
    this.themeService.toggleTheme();
  }

}
