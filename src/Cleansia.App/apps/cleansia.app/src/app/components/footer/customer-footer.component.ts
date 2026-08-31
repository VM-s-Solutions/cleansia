import { ChangeDetectionStrategy, Component, computed, inject } from '@angular/core';
import { RouterModule } from '@angular/router';
import { CustomerAuthService } from '@cleansia/customer-services';
import { ThemeService } from '@cleansia/services';
import { TranslatePipe } from '@ngx-translate/core';
import {
  CleansiaBrandNameComponent,
  CleansiaButtonComponent,
  CleansiaLanguageSwitcherComponent,
} from '@cleansia/components';

@Component({
  selector: 'cleansia-customer-footer',
  templateUrl: './customer-footer.component.html',
  standalone: true,
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [RouterModule, TranslatePipe, CleansiaBrandNameComponent, CleansiaButtonComponent, CleansiaLanguageSwitcherComponent],
})
export class CleansiaCustomerFooterComponent {
  private readonly authService = inject(CustomerAuthService);
  private readonly themeService = inject(ThemeService);

  // Same source of truth as the header control, so the two never disagree.
  readonly isDarkMode = computed(() => this.themeService.currentTheme() === 'dark');

  currentYear = new Date().getFullYear();

  // Hide the guest lookup link for logged-in users — they have /orders.
  // Reactive on the service signal so the footer updates immediately on
  // sign-in/sign-out without remount.
  readonly isAnonymous = computed(() => !this.authService.isLoggedIn());

  toggleTheme(): void {
    this.themeService.toggleTheme();
  }

}
