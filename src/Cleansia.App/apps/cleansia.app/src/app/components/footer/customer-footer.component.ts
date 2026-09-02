import { ChangeDetectionStrategy, Component, computed, inject } from '@angular/core';
import { RouterModule } from '@angular/router';
import { CustomerAuthService } from '@cleansia/customer-services';
import { ThemeService } from '@cleansia/services';
import { TranslatePipe } from '@ngx-translate/core';
import {
  CleansiaButtonComponent,
  CleansiaLanguageSwitcherComponent,
  CookieConsentService,
} from '@cleansia/components';

@Component({
  selector: 'cleansia-customer-footer',
  templateUrl: './customer-footer.component.html',
  standalone: true,
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [RouterModule, TranslatePipe, CleansiaButtonComponent, CleansiaLanguageSwitcherComponent],
})
export class CleansiaCustomerFooterComponent {
  private readonly authService = inject(CustomerAuthService);
  private readonly themeService = inject(ThemeService);
  private readonly cookieConsent = inject(CookieConsentService);

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
