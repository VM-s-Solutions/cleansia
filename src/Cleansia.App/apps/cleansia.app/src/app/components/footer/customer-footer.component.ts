import { ChangeDetectionStrategy, Component, computed, inject, input, signal } from '@angular/core';
import { FormsModule, NgForm } from '@angular/forms';
import { RouterModule } from '@angular/router';
import { CustomerAuthService } from '@cleansia/customer-services';
import { ThemeService } from '@cleansia/services';
import { SnackbarService } from '@cleansia/services';
import { TranslatePipe } from '@ngx-translate/core';
import {
  CleansiaBrandNameComponent,
  CleansiaButtonComponent,
  CleansiaLanguageSwitcherComponent,
} from '@cleansia/components';
import { PromoRequestFacade } from './promo-request.facade';

@Component({
  selector: 'cleansia-customer-footer',
  templateUrl: './customer-footer.component.html',
  standalone: true,
  changeDetection: ChangeDetectionStrategy.OnPush,
  providers: [PromoRequestFacade],
  imports: [FormsModule, RouterModule, TranslatePipe, CleansiaBrandNameComponent, CleansiaButtonComponent, CleansiaLanguageSwitcherComponent],
})
export class CleansiaCustomerFooterComponent {
  private readonly snackbarService = inject(SnackbarService);
  private readonly authService = inject(CustomerAuthService);
  private readonly themeService = inject(ThemeService);

  // Same source of truth as the header control, so the two never disagree.
  readonly isDarkMode = computed(() => this.themeService.currentTheme() === 'dark');

  showNewsletter = input(false);
  currentYear = new Date().getFullYear();

  // Hide the guest lookup link for logged-in users — they have /orders.
  // Reactive on the service signal so the footer updates immediately on
  // sign-in/sign-out without remount.
  readonly isAnonymous = computed(() => !this.authService.isLoggedIn());

  readonly promo = inject(PromoRequestFacade);
  readonly consented = signal(false);

  toggleTheme(): void {
    this.themeService.toggleTheme();
  }

  toggleConsent(): void {
    this.consented.update((v) => !v);
    this.promo.reset();
  }

  /**
   * Ask for a first-order promo code.
   *
   * This used to show a success toast and reset the form without sending
   * anything anywhere. The facade now reports the real state.
   */
  submitRequest(form: NgForm): void {
    if (!this.consented()) {
      this.snackbarService.showErrorTranslated('pages.home.footer.promo_consent_required');
      return;
    }
    this.promo.request((form.value as { email?: string })?.email ?? '', this.consented());
  }
}
